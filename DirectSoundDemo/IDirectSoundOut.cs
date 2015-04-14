using System;
using NAudio.Wave;

namespace DirectSoundDemo
{
	public interface IDirectSoundOut{
		PlaybackState PlaybackState {get;}

		void Init(IWaveProvider waveProvider);
		void Play();
		void Stop();
		void Dispose();
	}
}