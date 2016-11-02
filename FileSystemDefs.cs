using System;
using System.IO;
using System.Collections.Generic; 
using System.Threading;

namespace CS422
{
	public abstract class Dir422
	{
		protected Dir422 parent;
		public abstract string Name { get; }
		public abstract IList<Dir422> GetDirs();
		public abstract IList<File422> GetFiles();
		public abstract Dir422 Parent { get; }
		public abstract bool ContainsFile(string fileName, bool recursive);
		public abstract bool ContainsDir(string dirName, bool recursive);
		public abstract Dir422 GetDir(string dirName);
		public abstract File422 GetFile(string fileName);
		public abstract File422 CreateFile(string fileName);
		public abstract Dir422 CreateDir(string fileName);
	}
		
	public abstract class File422
	{
		protected Dir422 parent;
		public abstract string Name { get; }
		public abstract Stream OpenReadOnly();
		public abstract Stream OpenReadWrite();
		public abstract Dir422 Parent { get; }
	}

	public abstract class FileSys422
	{
		protected Dir422 root;
		public abstract Dir422 GetRoot();
		public virtual bool Contains(Dir422 directory) { return root.ContainsDir(directory.Name, true); }
		public virtual bool Contains(File422 file) { return root.ContainsFile(file.Name, true); }
	}

	public class StdFSDir : Dir422
	{
		private string path_name;

		public StdFSDir(Dir422 parent, string path_name)
		{
			this.parent = parent;
			this.path_name = path_name;
		}

		public override string Name 
		{ 
			get {  return Path.GetFileName(path_name); } 
		}  

		public override IList<Dir422> GetDirs()  
		{ 
			IList<Dir422> local_dirs = new List<Dir422>();
	
			foreach (string dir_path in Directory.GetDirectories(path_name))
			{
				Dir422 local_dir = new StdFSDir(this, dir_path);
				local_dirs.Add(local_dir);
			}

			return local_dirs;
		} 

		public override IList<File422> GetFiles()
		{ 
			IList<File422> local_files = new List<File422>();

			foreach (string file_path in Directory.GetFiles(path_name))
			{
				File422 local_file = new StdFSFile(this, file_path);
				local_files.Add(local_file);
			}

			return local_files;
		} 

		public override Dir422 Parent { get { return parent; } }

		public override bool ContainsFile(string fileName, bool recursive)
		{ 
			
			if (fileName == null || fileName == "" || fileName.Contains("/") || fileName.Contains(@"\")) { return false; }

			foreach (string file_path in Directory.GetFiles(path_name))
			{
				if (Path.GetFileName(file_path) == fileName) { return true; }
			}

			if (recursive)
			{
				foreach (Dir422 dir in GetDirs())
				{
					if (dir.ContainsFile(fileName, true)) { return true; } 
				}
			}

			return false;
		}

		public override bool ContainsDir(string dirName, bool recursive)
		{ 

			if (dirName == null || dirName == "" || dirName.Contains("/") || dirName.Contains(@"\")) { return false; }

			foreach (string dir_path in Directory.GetDirectories(path_name))
			{
				if (Path.GetFileName(dir_path) == dirName) { return true; }
			}

			if (recursive)
			{
				foreach (Dir422 dir in GetDirs())
				{
					if (dir.ContainsDir(dirName, true)) { return true; } 
				}
			}

			return false;
		}

		public override Dir422 GetDir(string dirName)
		{ 
			if (dirName == null || dirName == "" || dirName.Contains("/") || dirName.Contains(@"\")) { return null; }

			foreach (string dir_path in Directory.GetDirectories(path_name))
			{
				if (Path.GetFileName(dir_path) == dirName) { return new StdFSDir(this, dir_path); }
			}

			return null;
		}

		public override File422 GetFile(string fileName)
		{
			if (fileName == null || fileName == "" || fileName.Contains("/") || fileName.Contains(@"\")) { return null; }

			foreach (string file_path in Directory.GetFiles(path_name))
			{
				if (Path.GetFileName(file_path) == fileName) 
				{ 
					return new StdFSFile(this, file_path);
				}
			}

			return null;
		}

		public override File422 CreateFile(string fileName)
		{ 
			if (fileName == null || fileName == "" || fileName.Contains("/") || fileName.Contains(@"\")) { return null; }

			string file_path = path_name + "/" + fileName;

			try { File.Create(file_path).Dispose(); } 
			catch { return null; }

			return new StdFSFile(this, file_path);
		}

		public override Dir422 CreateDir(string dirName) 
		{
			if (dirName == null || dirName == "" || dirName.Contains("/") || dirName.Contains(@"\")) { return null; }

			string dir_path = path_name + "/" + dirName;

			try { Directory.CreateDirectory(dir_path); }
			catch { return null; }

			return new StdFSDir(this, dir_path);
		}
			
	}

	public class StdFSFile : File422
	{
		private string path_name;

		public StdFSFile(Dir422 parent, string path_name)
		{
			this.parent = parent;
			this.path_name = path_name;
		}

		public override string Name 
		{ 
			get {  return Path.GetFileName(path_name); } 
		}  

		public override Stream OpenReadOnly() 
		{
			FileStream fs = null;

			try { fs = File.Open(path_name, FileMode.Open, FileAccess.Read, FileShare.Read); }
			catch(Exception e) { Console.WriteLine(e.ToString()); }

			return fs;
		}

		public override Stream OpenReadWrite() 
		{ 
			FileStream fs = null;

			try { fs = File.Open(path_name, FileMode.Open, FileAccess.ReadWrite); }
			catch(Exception e) { Console.WriteLine(e.ToString()); }

			return fs;
		}

		public override Dir422 Parent { get { return parent; } }

	}

	public class StandardFileSystem : FileSys422
	{
		private StandardFileSystem(string path_name) 
		{ 
			root = new StdFSDir(null, path_name);
		}

		public override Dir422 GetRoot() { return root; }

		public static StandardFileSystem Create(string rootDir)
		{
			if (!Directory.Exists(rootDir)) { return null; }

			StandardFileSystem fs = new StandardFileSystem(rootDir);

			return fs;
		}
	}

	public class MemFSDir : Dir422
	{
		private string name;
		private IList<Dir422> dirs = new List<Dir422>();
		private IList<File422> files = new List<File422>();

		public MemFSDir(MemFSDir parent, string name)
		{
			this.parent = parent;
			this.name = name;
		}

		public override string Name { get { return name; } }

		public override IList<Dir422> GetDirs() { return dirs; }

		public override IList<File422> GetFiles() { return files; }

		public override Dir422 Parent { get { return parent; } }

		public override bool ContainsFile(string fileName, bool recursive) 
		{
			if (fileName == null || fileName == "" || fileName.Contains("/") || fileName.Contains(@"\")) { return false; }

			foreach (File422 file in GetFiles())
			{
				if (file.Name == fileName) { return true; }
			}

			if (recursive)
			{
				foreach (Dir422 dir in GetDirs())
				{
					if (dir.ContainsFile(fileName, true)) { return true; } 
				}
			}

			return false;
		}

		public override bool ContainsDir(string dirName, bool recursive)
		{
			
			if (dirName == null || dirName == "" || dirName.Contains("/") || dirName.Contains(@"\")) { return false; }

			foreach (Dir422 dir in GetDirs())
			{
				if (dir.Name == dirName) { return true; }
			}

			if (recursive)
			{
				foreach (Dir422 dir in GetDirs())
				{
					if (dir.ContainsDir(dirName, true)) { return true; } 
				}
			}

			return false;
		}

		public override Dir422 GetDir(string dirName)
		{
			if (dirName == null || dirName == "" || dirName.Contains("/") || dirName.Contains(@"\")) { return null; }

			foreach (Dir422 dir in GetDirs())
			{
				if (dir.Name == dirName) { return dir; }
			}

			return null;
		}

		public override File422 GetFile(string fileName)
		{
			if (fileName == null || fileName == "" || fileName.Contains("/") || fileName.Contains(@"\")) { return null; }

			foreach (File422 file in GetFiles())
			{
				if (file.Name == fileName) { return file; }
			}

			return null;
		}

		public override File422 CreateFile(string fileName) 
		{
			if (fileName == null || fileName == "" || fileName.Contains("/") || fileName.Contains(@"\")) { return null; }

			foreach (Dir422 dir in GetDirs())
			{
				if (dir.Name == fileName) { return null; }
			}
				
			foreach (File422 file in GetFiles())
			{
				if (file.Name == fileName) 
				{  
					files.Remove(file);
				}
			} 

			File422 new_file = new MemFSFile(this, fileName);
			files.Add(new_file);

			return new_file;
		}

		public override Dir422 CreateDir(string dirName)
		{
			if (dirName == null || dirName == "" || dirName.Contains("/") || dirName.Contains(@"\")) { return null; }

			foreach (Dir422 dir in GetDirs())
			{
				if (dir.Name == dirName) { return null; }
			}

			foreach (File422 file in GetFiles())
			{
				if (file.Name == dirName) { return null; }
			}
				
			Dir422 new_dir = new MemFSDir(this, dirName);
			dirs.Add(new_dir);

			return new_dir;
		}

	}

	public class MemFSFile : File422
	{
		private string name;
		private byte[] mem = new byte[10 * 1024];
		private readonly Object file_lock = new Object();
		private IList<MemoryStream> stream_reference_table = new List<MemoryStream>();

		public MemFSFile(MemFSDir parent, string name)
		{
			this.parent = parent;
			this.name = name;
		}

		public override string Name { get { return name; } }

		public override Stream OpenReadOnly() 
		{
			MemoryStream new_stream = null;

			lock(file_lock)
			{
RESTART_CHECK_OPEN_WRITE:
				foreach (MemoryStream mem_stream in stream_reference_table)
				{
					if (!mem_stream.CanRead && !mem_stream.CanWrite)
					{
						stream_reference_table.Remove(mem_stream);
						goto RESTART_CHECK_OPEN_WRITE;
					}

					if (mem_stream.CanWrite) { return null; }
				}

				new_stream = new MemoryStream(mem, false);
				stream_reference_table.Add(new_stream);
			}
			return new_stream;

		}

		public override Stream OpenReadWrite() 
		{
			MemoryStream new_stream = null;

			lock(file_lock)
			{
RESTART_CHECK_OPEN_ANY:
				foreach (MemoryStream mem_stream in stream_reference_table)
				{
					if (!mem_stream.CanRead && !mem_stream.CanWrite)
					{
						stream_reference_table.Remove(mem_stream);
						goto RESTART_CHECK_OPEN_ANY;
					}
					else
					{
						return null;
					}
				}

				new_stream = new MemoryStream(mem, true);
				stream_reference_table.Add(new_stream);
			}

			return new_stream;
		}

		public override Dir422 Parent { get { return parent; } }
	}

	public class MemoryFileSystem : FileSys422
	{
		public MemoryFileSystem() 
		{ 
			root = new MemFSDir(null, "root");
		}

		public override Dir422 GetRoot() { return root; }
	}
}
