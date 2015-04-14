using System;
using System.Threading;
using NAudio.Wave;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace DirectSoundDemo
{
	public class OpenALSoundOut : IDirectSoundOut
	{
		private static AudioContext context; // AudioContext must be unique, and hence, static
		private int sourceID; // source is the sound source, like an ID of the soundcard
		private int buffer_count;
		private int[] bufferIDs; // generating four buffers, so they can be played in sequence
		private int state; // current execution state, should be 4116 for ALSourceState.Stopped and 4114 for ALSourceState.Playing
		private int channels; // how many audio channels to allocate
		private int bitsPerSample; // default bits per sample
		private int sampleRate; // default audio rate
		private byte[] soundData;
		private ALFormat alFormat;
		private int milissecondsPerBuffer;
		private int bufferSize;
		private int currentBufferIndex;
		private Thread playThread;
		private int bufferReads;

		private IWaveProvider waveProvider;

		public PlaybackState PlaybackState {
			get{
				return PlaybackState.Stopped; // mock
			}
		}

		public OpenALSoundOut (){
		}

		public OpenALSoundOut (int latency){
		}

		public void Init(IWaveProvider waveProvider){
			OpenALSoundOut.context = new AudioContext ();
			sourceID = AL.GenSource (); // source is the sound source, like an ID of the soundcard
			buffer_count = 4;
			bufferIDs = AL.GenBuffers (buffer_count); // generating four buffers, so they can be played in sequence
			state = 4116; // current execution state, should be 4116 for ALSourceState.Stopped and 4114 for ALSourceState.Playing
			channels = 2; // how many audio channels to allocate. 1 for mono and 2 for stereo
			bitsPerSample = 16; // default bits per sample
			sampleRate = 44100; // default audio rate
			milissecondsPerBuffer = 2000;
			alFormat = OpenALSoundOut.GetSoundFormat (channels, bitsPerSample);
			bufferSize = (int)((bitsPerSample/8) * channels * sampleRate * (milissecondsPerBuffer/1000f));
			currentBufferIndex = 0;
			bufferReads = 0;

			this.waveProvider = waveProvider;
		}

		public void Play(){
			playThread = new Thread(new ThreadStart(PlaybackThreadFunc));
			// put this back to highest when we are confident we don't have any bugs in the thread proc
			playThread.Priority = ThreadPriority.Normal;
			playThread.IsBackground = true;
			playThread.Start();
		}

		public void Stop(){
			AL.SourceStop (sourceID);
		}

		public void Dispose(){
			AL.SourceStop (sourceID); // stop sound source
			AL.DeleteSource (sourceID); // delete it
			AL.DeleteBuffers (bufferIDs); // and also the buffers
			OpenALSoundOut.context.Dispose (); // if we where not using the "using" keyword for AudioContext, we would have to dispose the context manually
		}


		private static ALFormat GetSoundFormat (int channels, int bits)
		{
			switch (channels) {
				case 1:
				return bits <= 8 ? ALFormat.Mono8 : ALFormat.Mono16;
				case 2:
				return bits <= 8 ? ALFormat.Stereo8 : ALFormat.Stereo16;
				default:
				throw new NotSupportedException ("The specified sound format is not supported.");
			}
		}

		private void PlaybackThreadFunc(){
			for (int i = 0; i < bufferIDs.Length; i++) { // fill all the buffers first
				soundData = new byte[bufferSize];
				waveProvider.Read(soundData, 0, soundData.Length);
				AL.BufferData (bufferIDs [i], OpenALSoundOut.GetSoundFormat (channels, bitsPerSample), soundData, soundData.Length, sampleRate); // put it into the sound buffer
				bufferReads++;
				Console.WriteLine("read the "+bufferReads+"th wave chunk to buffer "+(i+1)+"/"+bufferIDs.Length+".");
			}

			bool firstPlay = true;
			AL.SourceQueueBuffer (sourceID, bufferIDs [currentBufferIndex]); // enqueues the first buffer
			Console.WriteLine ("playing buffer "+(currentBufferIndex+1)+" of "+bufferIDs.Length+".");
			AL.SourcePlay (sourceID); // and plays it

			do {
				AL.GetSource (sourceID, ALGetSourcei.SourceState, out state); // check the state of the audio source execution
				if ((ALSourceState)state != ALSourceState.Playing) { // if has already finished playing the last buffer
					AL.SourceUnqueueBuffer (sourceID); // unqueue the buffer
//						Console.WriteLine ("unqueued buffer: "+unqueuedBufferId);
					currentBufferIndex++;
					Console.WriteLine ("playing buffer " + (currentBufferIndex+1) +" of "+bufferIDs.Length);
					AL.SourceQueueBuffer (sourceID, bufferIDs [currentBufferIndex]); // enqueues the next buffer
					AL.SourcePlay (sourceID); // and plays it


					if(!firstPlay){ // read the next sound buffer unless this is the first
						waveProvider.Read(soundData, 0, soundData.Length);
						int bufferToWrite = currentBufferIndex-1; // determina o id do buffer a preencher como o do buffer anterior
						bufferToWrite = bufferToWrite > -1 ? bufferToWrite : bufferIDs.Length-1; // se o buffer a preencher é o último, ao invés da posição -1 fica como o tamanho do vetor menos um
						AL.BufferData (bufferIDs [bufferToWrite], OpenALSoundOut.GetSoundFormat (channels, bitsPerSample), soundData, soundData.Length, sampleRate); // put it into the sound buffer
						bufferReads++;
						Console.WriteLine("read the "+bufferReads+"th wave chunk to buffer "+(bufferToWrite+1)+"/"+bufferIDs.Length+".");
						if(currentBufferIndex == bufferIDs.Length-1){ // if we are playing the last buffer
							currentBufferIndex = -1; // go back to the first, since we are streaming the audio
						}
						state = (int)ALSourceState.Playing;
					}else{
						Console.WriteLine("first play");
					}
				}

				if((ALSourceState)state == ALSourceState.Playing){ // if is still playing something,
					Thread.Sleep (1); // wait a little bit, by 25ms
				}
				firstPlay = false; // the first buffer play is no more
			} while ((ALSourceState)state == ALSourceState.Playing);
		}

		private byte[] fillArrayWithZeroes(byte[] array){
			for(int i = 0; i < array.Length; i++){
				array [i] = 0;
			}
			return array;
		}
	}
}
