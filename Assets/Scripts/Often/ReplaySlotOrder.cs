using System;
using System.IO;

namespace Assets.Scripts.Often
{
    // A complete snapshot is made before touching any slot. An interrupted move
    // rolls back on next open, including empty slots and title-only slots.
    public static class ReplaySlotOrder
    {
        static readonly string[] Extensions={".bin",".title.txt"};
        static string FileName(int slot,string ext)=>$"slot{slot+1:00}{ext}";
        public static void Recover(string directory)
        {
            string backup=Path.Combine(directory,".slot-move");
            string marker=Path.Combine(backup,"ready");
            if(!File.Exists(marker))return;
            int count=int.Parse(File.ReadAllText(marker));
            for(int i=0;i<count;i++)foreach(string ext in Extensions)
            {
                string name=FileName(i,ext), source=Path.Combine(backup,name), target=Path.Combine(directory,name);
                if(File.Exists(source))File.Copy(source,target,true);else if(File.Exists(target))File.Delete(target);
            }
            File.Delete(marker);Directory.Delete(backup,true);
        }
        public static void Move(string directory,int from,int to,int count)
        {
            if(from<0||to<0||from>=count||to>=count)throw new ArgumentOutOfRangeException();
            if(from==to)return;
            Recover(directory);
            string backup=Path.Combine(directory,".slot-move");
            if(Directory.Exists(backup))Directory.Delete(backup,true);
            Directory.CreateDirectory(backup);
            for(int i=0;i<count;i++)foreach(string ext in Extensions){string name=FileName(i,ext),source=Path.Combine(directory,name);if(File.Exists(source))File.Copy(source,Path.Combine(backup,name));}
            File.WriteAllText(Path.Combine(backup,"ready"),count.ToString());
            try
            {
                for(int i=Math.Min(from,to);i<=Math.Max(from,to);i++)foreach(string ext in Extensions)
                {
                    int original=i==to?from:from<to?i+1:i-1;
                    string source=Path.Combine(backup,FileName(original,ext)),target=Path.Combine(directory,FileName(i,ext));
                    if(File.Exists(source))File.Copy(source,target,true);else if(File.Exists(target))File.Delete(target);
                }
            }
            catch{Recover(directory);throw;}
            File.Delete(Path.Combine(backup,"ready"));Directory.Delete(backup,true);
        }
    }
}
