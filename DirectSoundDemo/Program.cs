using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenTK.Audio;

namespace DirectSoundDemo
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
		{
			if (Type.GetType ("Mono.Runtime") != null) {
				const string message = "It seems you are running under linux or mac.\r\nIf you do not have OpenAL installed on your system, the program will crash on playing.";
				MessageBox.Show(message);
			}

			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);
            Application.Run(new MainForm());
        }
    }
}
