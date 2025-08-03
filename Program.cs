// #define TESTS
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
rendering to WAV.
 
instead, it might be possible to apply a loudness
contour like ISO 226:2023 based on the frequency
of the MIDI "note"

#region PITCH_RELATED is where this idea starts
#region ITU-R468 is a common standard.  I have a 
python conversion right now, but I believe the C# 
versions would be almost:

	R{ITU}(f) = 1.246332637532143e-4 * f / (sqrt ( (h_1(f))^2 + (h_2(f))^2 ) );

	ITU(f) = 18.2 + 20*Math.Log((R(ITU)(f), 10);

	h_1(f) = -4.737338981378384e-24 * f^6 + 2.043828333606125e-15 * f^4 - 1.363894795463638e-7 * f^2 + 1 };

	h_2(f)=1.306612257412824e-19 * f^5 - 2.118150887518656e-11 \, f^3 + 5.559488023498642e-4 * f;

which would translate well to C/C++ - based on 
https://en.wikipedia.org/wiki/ITU-R_468_noise_weighting
*/

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;

namespace ReplayGainMidi
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if TESTS
            RunTests();
            return;
#else
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
#endif
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

        #region PITCH_RELATED

        // in case another reference pitch is required
        const double A4Frequency = 440.0;
        static Dictionary<int, double> Frequencies = new();

        //  the note A4 is 69 and increases by one for each equal tempered semitone
        static double MidiNumberToFrequency(int number)
        {
            if (Frequencies.ContainsKey(number))
                return Frequencies[number];

            //  fm  =  2^((m−69)/12)(440 Hz)
            double frequency = A4Frequency * Math.Pow(2, (number - 69) / 12.0);
            Frequencies[number] = frequency;

            return frequency;
        }

#if TESTS
        static void RunTests()
        {
            // 60	C4 (Middle C)	261.626
            var freq = MidiNumberToFrequency(60);
            if (Math.Round(freq, 3, MidpointRounding.AwayFromZero) != 261.626)
                throw new Exception("Math result unexpected");

            var filtered = r468(freq, "1khz", "db");

            // 1khz means 1kHz and 12.5 kHz are zeros
            freq = 1000.0;
            filtered = r468(freq, "1khz", "db");
            if (Math.Round(filtered, 1) != 0.0)
                throw new Exception("ITU-R_468 result unexpected");

            freq = 12500;
            filtered = r468(freq, "1khz", "db");
            if (Math.Round(filtered, 1) != 0.0)
                throw new Exception("ITU-R_468 result unexpected");

            // extreme example
            freq = 31500.0;
            filtered = r468(freq, "1khz", "db");
            if (Math.Round(filtered, 1) != (-42.7))
                throw new Exception("ITU-R_468 result unexpected");
        }
#endif

        #region ITU-R468

        // ref python ex https://github.com/avtools-io/itu-r-468-weighting/blob/master/itu_r_468_weighting/filter.py
        // and https://en.wikipedia.org/wiki/ITU-R_468_noise_weighting
        // this is not implemented, but it is tested.  I believe
        // the "factor" return should give a multiplier, but the test data
        // I have independent of the implementation is in dB.

        static double r468(double frequency_hz, string khz_option, string returns)
        {
            switch(returns)
            {
                case "db":
                case "factor":
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(returns));
            }

            switch (khz_option)
            {
                case "1khz":
                case "2khz":
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(returns));
            }

            if (frequency_hz <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequency_hz));

            // calculate exponents once
            double f1 = frequency_hz;
            double f2 = f1 * f1;
            double f3 = f1 * f2;
            double f4 = f2 * f2;
            double f5 = f1 * f4;
            double f6 = f3 * f3;

            double h1 =
                (-4.7373389813783836e-24 * f6)
                + (2.0438283336061252e-15 * f4)
                - (1.363894795463638e-07 * f2)
                + 1;

            double h2 =
                (1.3066122574128241e-19 * f5)
                - (2.1181508875186556e-11 * f3)
                + (0.0005559488023498643 * f1);

            double r_itu = (0.0001246332637532143 * f1) / Math.Sqrt(h1 * h1 + h2 * h2);

            if (returns == "db")
            {
                if (khz_option == "1khz")
                {
                    if (r_itu == 0.0)
                        return 0.0;
                    else
                        return DB_GAIN_1KHZ + 20.0 * Math.Log(r_itu, 10);
                }
                else if (khz_option == "2khz")
                {
                    if (r_itu == 0.0)
                        return 0.0;
                    else
                        return DB_GAIN_2KHZ + 20.0 * Math.Log(r_itu, 10);
                }
            }
            else if (returns == "factor")
            {
                if (khz_option == "1khz")
                    return FACTOR_GAIN_1KHZ * r_itu;
                else if (khz_option == "2khz")
                    return FACTOR_GAIN_2KHZ * r_itu;
            }

            throw new Exception("Unexpected ITU-R 468 result");
        }

        #region CONSTANTS
        /*
            # DB_GAIN_1KHZ was first determined with the multiplication factor of 8.1333
            # (+18.20533583440004 dB). From there on the value was modified to find
            # a better one, by converging the distance to 0 at 1000 Hz and 12500 Hz, having
            # the same value for both: r468(1000, "1khz") == r468(12500, "1khz")
            DB_GAIN_1KHZ = 18.246265068039158

            # DB_GAIN_2KHZ was determined by substracting the dB value at 2000 Hz (of
            # the "1khz" option result) from and adding the value at 1000 Hz (of the "1khz"
            # option result) to the value of DB_GAIN_1KHZ:
            # DB_GAIN_2KHZ = DB_GAIN_1KHZ - r468(2000, "1khz") + r468(1000, "1khz")
            DB_GAIN_2KHZ = 12.617052124255618

            # Gain factor, "1khz" option
            FACTOR_GAIN_1KHZ = 10 ** (DB_GAIN_1KHZ / 20)

            # Gain factor, "2khz" option
            FACTOR_GAIN_2KHZ = 10 ** (DB_GAIN_2KHZ / 20)        
        */
        static double DB_GAIN_1KHZ = 18.246265068039158;
        static double DB_GAIN_2KHZ = 12.617052124255618;
        static double FACTOR_GAIN_1KHZ = Math.Pow(10, (DB_GAIN_1KHZ / 20));
        static double FACTOR_GAIN_2KHZ = Math.Pow(10, (DB_GAIN_2KHZ / 20));

        #endregion CONSTANTS
        #endregion ITU-R468

        #endregion PITCH_RELATED
    }
}
