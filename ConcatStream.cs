using System;
using System.IO;
using System.Text;

namespace CS422
{
	public class ConcatStream : Stream
	{
		private Stream first;
		private Stream second;
		private long position;
		private long fixedLength = -1;

		public ConcatStream(Stream first, Stream second)
		{
			if (!first.CanSeek) { throw new NotSupportedException(); }

			this.first = first;
			this.second = second;

			position = 0;

			if (CanSeek) 
			{
				fixedLength = first.Length + second.Length;
			}
		}

		public ConcatStream(Stream first, Stream second, long fixedLength)
		{
			if (!first.CanSeek) { throw new NotSupportedException(); }
			this.first = first;
			this.second = second;
			this.fixedLength = fixedLength;
			position = 0;
		}
			
		public override bool CanRead 
		{
			get { return first.CanRead && second.CanRead? true : false; }
		}

		public override bool CanSeek
		{
			get { return first.CanSeek && second.CanSeek? true : false; }
		}

		public override bool CanWrite
		{
			get { return first.CanWrite && second.CanRead? true : false; }
		}

		public override bool CanTimeout
		{
			get { return first.CanTimeout && second.CanTimeout? true : false; }
		}

		public override long Length
		{
			get 
			{ 
				if (fixedLength == -1) { throw new NotSupportedException(); }
				return fixedLength; 
			}
		}

		public override long Position
		{
			get 
			{ 
				if (!CanSeek) { throw new NotSupportedException(); }
				return position; 
			}
			set 
			{
				if (!CanSeek) { throw new NotSupportedException(); }
				if (value < 0) { position = 0; }
				else if (Length < value) { position = Length; }
				else { position = value; }
			}
		}

		public override void SetLength(long value)
		{
			if (fixedLength == -1) { throw new NotSupportedException(); }
			if (value < 0) { throw new ArgumentOutOfRangeException(); }
			fixedLength = value;
		}

		public override void Flush()
		{
			throw new NotSupportedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (!CanSeek) { throw new NotSupportedException(); }
			if (origin == SeekOrigin.Begin) { Position = offset; }
			if (origin == SeekOrigin.End) { Position = (Length - 1) + offset; }
			if (origin == SeekOrigin.Current) { Position = Position + offset; }
			if (Position < first.Length)
			{
				first.Position = Position;
				second.Position = 0;
			}
			else 
			{
				first.Position = first.Length;
				second.Position = Position - first.Length;
			}

			return Position;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (!CanRead) { throw new NotSupportedException(); }

			int n = 0;
			int bytesRead = 0;

			if (CanSeek)
			{
				if (Length - Position < count)
				{
					count = (int)Length - (int)Position;
				}
				if (Position < first.Length)
				{
					n = first.Read(buffer, offset, count);
					bytesRead = n;
					Position += n;
				}
				if (first.Length <= Position)
				{
					n = second.Read(buffer, offset + bytesRead, count - bytesRead);

		

					bytesRead += n;
					Position += n;
				}
				while (bytesRead < Length && bytesRead < count)
				{
					buffer[offset + bytesRead++]=0;
				}
			}
			else
			{
				bytesRead += first.Read(buffer, offset, count);
				bytesRead += second.Read(buffer, offset + bytesRead, count - bytesRead);
			}

			return bytesRead;
		}


		public override void Write(byte[] buffer, int offset, int count) 
		{
			if (!CanWrite) { throw new NotSupportedException(); }

			int bytesWritten = 0;

			if (CanSeek)
			{
				if (Position < first.Length)
				{
					int remain = (int)first.Length - (int)Position;
					if (count < remain)
					{
						first.Write(buffer, offset, count);
						bytesWritten += count;
						Position += count;
						count = 0;
					}
					else
					{
						first.Write(buffer, offset, remain);
						bytesWritten += remain;
						Position += remain;
						count -= remain;
					}
					second.Position = 0;
				}
				if (first.Length <= Position)
				{
					second.Write(buffer, offset + bytesWritten, count);
					Position += count;
				}
				fixedLength = first.Length + second.Length;
			}
			else
			{
				second.Write(buffer, offset, count);
			}
		}
	}
}