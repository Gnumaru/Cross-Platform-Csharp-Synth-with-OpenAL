Cross Platform C# Synth with OpenAL
=========

This project is an adaptation of "C# Synth Project":

https://csharpsynthproject.codeplex.com/

It adapts the demo application, a simple midi player, to use "OpenAL Soft" (an implementation of the OpenAL specification, and a fork from the original "Sample Implementation") instead of Microsoft's DirectAudio:

http://kcat.strangesoft.net/openal.html

So that it can become cross-platform. Since I adapted the demo application, not the synth library, **this is not a fork of "C# Synth Project"**, it relies on the vanilla "C# Synth Project" library.

The "C# Synth Project" does not relies directly on DirectAudio by itself, but it uses NAudio for Audio rendering:

https://github.com/naudio/NAudio

NAudio, by its turn, relies on DirectAudio and thus can not be used on Linux, Mac OS or any non-Microsoft OS.

This adaptation I made also does not handle OpenAL (which is a native library) directly. Instead, it uses OpenTK:

https://github.com/opentk/opentk

which is a dotnet wrapper around OpenAL, OpenGL and OpenCL.

The demo app still needs NAudio.dll for some things, but the audio is played OpenAL and thus it works as expected on linux and mac.

The Adaptation is in it's earliest stage, but it is already able to play midi files.

Unfortunately, the "C# Synth Project" code on codeplex's source control is outdated. The latest source can only be downloaded on the download section, in a zip package containing the sources alongside with the binaries.

For convenience, I added a default midi sound bank and a midi file for testing.
