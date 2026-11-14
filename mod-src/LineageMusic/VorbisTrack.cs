using System;
using System.IO;
using System.Threading;
using NVorbis;

namespace GhostLineageMusic {
    internal sealed class VorbisTrack {
        internal readonly int Channels, Rate, Frames;
        internal readonly float[] Samples;

        VorbisTrack(int channels, int rate, int frames, float[] samples) {
            Channels = channels;
            Rate = rate;
            Frames = frames;
            Samples = samples;
        }

        internal static VorbisTrack Read(string path, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            using (var input = OpenOgg(path))
            using (var reader = new VorbisReader(input, false)) {
                int channels = reader.Channels;
                int rate = reader.SampleRate;
                long frames = reader.TotalSamples;
                if (channels < 1 || channels > 2 || rate < 8000 || rate > 96000 ||
                    frames <= 0 || frames * (double)channels > 48_000_000)
                    throw new InvalidDataException("Unsupported music stream size or format.");

                var samples = new float[checked((int)frames * channels)];
                int read = 0;
                while (read < samples.Length) {
                    cancellation.ThrowIfCancellationRequested();
                    int count = reader.ReadSamples(samples, read, Math.Min(32768, samples.Length - read));
                    if (count == 0)
                        break;
                    read += count;
                }
                if (read == 0 || read % channels != 0)
                    throw new InvalidDataException("No complete audio frames.");
                if (read != samples.Length)
                    Array.Resize(ref samples, read);
                return new VorbisTrack(channels, rate, read / channels, samples);
            }
        }

        static Stream OpenOgg(string path) {
            using (var source = File.OpenRead(path)) {
                if (source.Length < 58 || source.Length > 48_000_000)
                    throw new InvalidDataException("Unsupported Ogg file size.");
                var data = new byte[(int)source.Length];
                int offset = 0;
                while (offset < data.Length) {
                    int count = source.Read(data, offset, data.Length - offset);
                    if (count == 0)
                        throw new EndOfStreamException();
                    offset += count;
                }
                // The client replaces the first capture pattern and retains the original Ogg page CRC.
                if (data[0] == 'L' && data[1] == '2' && data[2] == 'S' && data[3] == 'D') {
                    data[0] = (byte)'O';
                    data[1] = (byte)'g';
                    data[2] = (byte)'g';
                    data[3] = (byte)'S';
                }
                return new MemoryStream(data, false);
            }
        }
    }
}
