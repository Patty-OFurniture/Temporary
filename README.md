PoC for near-realtime normalization of old school game audio. It's possible.

This is a proof-of-concept for DSDA Doom, from the impping stream comments about normalizing audio between tracks. It is based on the restrictions of how WAD files work i.e. Loading lumps in memory. Streamer mentioned he had found ReplayGain for MIDI, which I could not find. That would solve the problem of streaming while using local .mid files or audio paks.



It's C#, Visual Studio is free and you can use other tools, but if you can read C/C++/Java it should mostly make sense.


The problem to be solved is that playing a a new WAD would require extracting the MIDI lumps, analyzing, and replacing those lumps. So this is an example of gathering events in memory, grouping them by event time, totaling the combined velocities, and calculating some statistics.


That's the basic idea


It is fast, and can be implemented by reading the MAP01 audio lump as a baseline. Additional maps can be calculated as they are loaded, and have gain applied relative to MAP01.

Based on prboom2\src\MUSIC\musicplayer.h dsda can use the results to call music_player_t.setvolume(v) with a scale from 0-15 based on the gain to be applied, and I assume based on the current volume.

This example shows how quickly it can be done, as well as statistics based on a naive velocity sum.

It uses C# and Melanchall.DryWetMidi to load and parse midi files. DSDA is written in C, with the audio passed through the musicplayer interface, so not the same. midifile.h / midifile.c does have a parser, but I didn't put time into getting that to work. It's a PoC to make sure this is a worthwhile path.

