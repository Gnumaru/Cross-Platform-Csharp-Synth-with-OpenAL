using AudioSynthesis;
using System.IO;

namespace DirectSoundDemo
{
    public class MyFile : IResource
    {
        private string fileName;
        public MyFile(string fileName)
        {
            this.fileName = fileName;
        }
        public string GetName() { return fileName; }
        public bool DeleteAllowed() { return true; }
        public bool ReadAllowed() { return true; }
        public bool WriteAllowed() { return true; }
        public void DeleteResource() { File.Delete(fileName); }
        public Stream OpenResourceForRead() { return File.Open(fileName, FileMode.Open, FileAccess.Read); }
        public Stream OpenResourceForWrite() { return File.Open(fileName, FileMode.Create, FileAccess.Write); }
    }
}
