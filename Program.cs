/*
This is a proof-of-concept for DSDA Doom, from
coincident's impping stream comments about
normalizing audio between tracks.  Is is 
based on the restrictions of how WAD files work
i.e. Loading lumps in memory.  Coin mentioned
he had found ReplayGain for MIDI, which I
could not find.  That would solve the problem of
streaming while using local .mid files or
audio paks.

The problem is still that a new WAD would require
extracting the MIDI lumps, analyzing, and replacing
those lumps. So this is an example of gathering
events in memory, grouping them by event time,
totaling the combined velocities, and calculating
some statistics.

It is fast, and can be implemented by reading the
MAP01 audio lump as a baseline.  Additional maps
can be calculated as they are loaded, and have
gain applied relative to MAP01.

Based on prboom2\src\MUSIC\musicplayer.h
dsda can use the results to call 
music_player_t.setvolume(v) with a scale from 
0-15 based on the gain to be applied, and I assume
based on the current volume.


This example shows how quickly it can be done, 
as well as statistics based on a naive velocity 
sum.

It uses C# and Melanchall.DryWetMidi to load and 
parse midi files.  DSDA is written in C, with the 
audio passed through the musicplayer interface,
so not the same.  midifile.h / midifile.c does
have a parser, but I didn't put time into
getting that to work.  It's a PoC to make sure
this is a worthwhile path.


TODO: EBU R 128 aka ITU-R BS.1770
but actual "loudness" measures might require
rendering to WAV
 
instead, it might be possible to apply a loudness
contour like ISO 226:2023 based on the frequency
of the MIDI "note"


*/

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace ReplayGainMidi
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Please specify a file");
            }
            else if (Directory.Exists(args[0]))
            {
                foreach(var file in Directory.GetFiles(args[0], "*.mid"))
                {
                    DumpMidi(file);
                }
            }
            else if (File.Exists(args[0]))
            {
                DumpMidi(args[0]);
            }
            else
            {
                Console.WriteLine("Please specify a file");
            }
        }


        static void DumpMidi(string file)
        {
            Console.WriteLine($"Processing: {file}");

            var midiFile = MidiFile.Read(file);

            // midiFile.MergeObjects(
            //    Melanchall.DryWetMidi.Interaction.ObjectType.Chord |
            //    Melanchall.DryWetMidi.Interaction.ObjectType.TimedEvent | 
            //    Melanchall.DryWetMidi.Interaction.ObjectType.Note);

            // midiFile.Sanitize();

            var volumes = new List<int>();

            var chords = midiFile.GetChords();

            var groups = chords.GroupBy(c => c.Time).Select(g => g);

            foreach (var chord in groups)
            {
                //if (chord.Count() != 1)
                //    System.Diagnostics.Debugger.Break();

                int volume = 0;
                foreach (var c in chord)
                {
                    foreach (var n in c.Notes)
                    {
                        volume += n.Velocity;
                    }
                }
                //Console.WriteLine(volume.ToString());

                if (volume < 1)
                    System.Diagnostics.Debugger.Break();

                volumes.Add(volume);
            }

            Console.WriteLine($"AVG:{volumes.Average()}");
            Console.WriteLine($"Max:{volumes.Max()}");
            Console.WriteLine($"Min:{volumes.Min()}");
            Console.WriteLine($"RMS:{RMS_Value(volumes)}");
            Console.WriteLine();

            // CsvSerializer.SerializeToCsv(midiFile, file + ".csv", false);
        }


        // https://eddiejackson.net/wp/?page_id=20156
        // Root Mean Square  
        static float RMS_Value(List<int> arr, int n = 0)
        {
            if (n == 0)
                n = arr.Count;

            int square = 0;
            float mean, root = 0;

            // Calculate square
            for (int i = 0; i < n; i++)
            {
                square += (int)Math.Pow(arr[i], 2);
            }

            // Calculate Mean
            mean = (square / (float)(n));

            // Calculate Root
            root = (float)Math.Sqrt(mean);

            return root;
        }

    }
}

