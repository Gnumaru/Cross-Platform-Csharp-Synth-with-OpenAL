using System;
using NAudio.Wave;

namespace DirectSoundDemo
{
	public class DirectSoundOutBroker : IDirectSoundOut{
		private DirectSoundOut direct_out;

		public DirectSoundOutBroker(){
			direct_out = new DirectSoundOut (Properties.Settings.Default.Latency);
		}

		public DirectSoundOutBroker(int latency){
			direct_out = new DirectSoundOut (latency);
		}

		public PlaybackState PlaybackState {
			get{
				return direct_out.PlaybackState;
			}
		}

		public void Init(IWaveProvider waveProvider){
			direct_out.Init (waveProvider);
		}

		public void Play(){
			direct_out.Play ();
		}

		public void Stop(){
			direct_out.Stop ();
		}

		public void Dispose(){
			direct_out.Dispose ();
		}
	}
}
