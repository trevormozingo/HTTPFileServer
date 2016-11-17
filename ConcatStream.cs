using System;
using System.IO;
using System.Text;

namespace CS422
{
	public class ConcatStream : Stream
	{
		private Stream first;
		private Stream second;

		private long position = 0;						//the concatstream position (entire stream)
		public long secondPosition = 0;					//use secondPosition for the event that stream 2 is not seekable
		private long fixedLength = -1;					//mark if length is supported (if seeking in 2 is supported)
		private bool expandable = false;				//mark if stream is exapandable (used constructor 1)

		public ConcatStream(Stream first, Stream second)						//expandable stream
		{
			if (!first.CanSeek) { throw new NotSupportedException(); }			//stream 1 must have a length

			expandable = true;

			this.first = first;
			this.second = second;

			if (CanSeek) 														//length exists otherwise it is not supported
			{
				fixedLength = first.Length + second.Length;						
			}
		}

		public ConcatStream(Stream first, Stream second, long fixedLength)		//using this constructor, the streams cannot expand
		{
			if (!first.CanSeek) { throw new NotSupportedException(); }			//stream 1 must have a length

			this.first = first;
			this.second = second;

			this.fixedLength = fixedLength;										//set fixed length (will never change)
		}
			
		public override bool CanRead { get { return first.CanRead && second.CanRead? true : false; } }
		public override bool CanSeek { get { return first.CanSeek && second.CanSeek? true : false; } }
		public override bool CanWrite { get { return first.CanWrite && second.CanRead? true : false; } }
		public override bool CanTimeout { get { return first.CanTimeout && second.CanTimeout? true : false; } }
		public override void SetLength(long value) { throw new NotSupportedException(); }
		public override void Flush() { throw new NotSupportedException(); }

		public override long Length
		{
			get 
			{ 
				if (fixedLength == -1) { throw new NotSupportedException(); }			//if we used the first constructor & stream2 has no length -> not supported
				return fixedLength; 													//otherwise return the length of the streams
			}
		}

		public override long Position													
		{
			get 
			{ 																			//unseekable stream -> position will move left to right
				return position; 														//return the position
			}

			set 
			{
				if (!CanSeek) { throw new NotSupportedException(); }					//cannot seek within not seekable stream

				if (value < 0) { position = 0; }										//cap to zero, should never have to worry about this case

				else { position = value; }

				if (position <= first.Length)		//reset first stream position
				{
					first.Position = position;

					if (second.CanSeek)	 { second.Position = 0; }						//if the second stream is seekable
					secondPosition = 0;
				}

				else 								//reset second stream position
				{
					first.Position = first.Length;
						
					if (second.CanSeek)	 { second.Position = position - first.Length; }	//if the second stream is seekable
					secondPosition = position - first.Length;
				}
			}
		}
			
		/*
			This is only accessable to the class itself (private), this is to ensure 
			that nothing outside the class can seek if it is not seekable (see above)  */
		private long Position2															//just to be used from within the class								
		{
			get 
			{ 																			//unseekable stream -> position will move left to right
				return position; 														//return the position
			}

			set 
			{
				if (value < 0) { position = 0; }										//cap to zero, should never have to worry about this case

				else { position = value; }

				if (position <= first.Length)						//reset first stream position
				{
					first.Position = position;

					if (second.CanSeek)	 { second.Position = 0; }	//if the second stream is seekable
					secondPosition = 0;
				}

				else 												//reset second stream position
				{
					first.Position = first.Length;

					if (second.CanSeek)	 { second.Position = position - first.Length; }	//if the second stream is seekable
					secondPosition = position - first.Length;
				}
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (!CanSeek) { throw new NotSupportedException(); }						//if you cant seek --> not supported

			if (origin == SeekOrigin.Begin) { Position2 = offset; }

			if (origin == SeekOrigin.End) 
			{ 
				Position2 = Length + offset; 
			}

			if (origin == SeekOrigin.Current) { Position2 = Position2 + offset; }

			return Position2;
		}

		//seekable unrestricted (first constructor, both seek), seekable restricted (second constructor, both seek)
		//unseekable not supported (first constructor, not 2), unseekable restricted (second constuctor, not 2)

		public override int Read(byte[] buffer, int offset, int count)		//read is forward read only
		{
			if (!CanRead) { throw new NotSupportedException(); }	//if you cannot read --> not supported

			int n = 0;
			int bytesRead = 0;

			if (-1 != fixedLength)									//if a length exists
			{
				if (Length - Position2 < count)						//if count is greater than the length 
				{
					count = (int)Length - (int)Position2;			//cap count to the bytes remaining
				}
			}

			if (Position2 < first.Length)						//if in the first stream
			{
				n = first.Read(buffer, offset, count);			//read (at most) the rest of the first stream
				bytesRead = n;
				Position2 += n;
			}

			if (first.Length <= Position2)						//now read bytes remaining in second stream
			{
				n = second.Read(buffer, offset + bytesRead, count - bytesRead);
				bytesRead += n;
				Position2 += n;									
			}
				
			return bytesRead;
		}
			
		public override void Write(byte[] buffer, int offset, int count) 
		{
			if (!CanWrite) { throw new NotSupportedException(); }

			int bytesWritten = 0;

			if (!expandable)												//if the stream has a fixed length (second constructor)
			{
				if ((Length - Position2) < count) { throw new NotSupportedException(); }	//if the bytes to write is greater than the length, throw an error
			}
			else 															//if it is expandable									
			{
				if (fixedLength != -1 && (Length - Position2) < count) 		//expandable and suported
				{
					fixedLength += (count - (Length - Position2));			//expand if necessary
				}
			}

			if (Position2 < first.Length)									//if you are in the first half
			{
				int remain = (int)first.Length - (int)Position2;			//get space remaining in the first half

				if (count < remain)											//if just writing to the first half										
				{
					first.Write(buffer, offset, count);
					bytesWritten += count;
					Position2 += count;
					count = 0;
				}
				else 														//if writing to the first & second half
				{
					first.Write(buffer, offset, remain);
					bytesWritten += remain;
					Position2 += remain;
					count -= remain;
				}
			}

			if (first.Length <= Position2)									//if writing to second half
			{
				if (-1 != fixedLength && !second.CanSeek)					//if there is a fixed length and the second cannot seek
				{
					if (secondPosition != Position2 - first.Length) { throw new NotSupportedException(); }	//cannot think of a case when this would happen
				}

				second.Write(buffer, offset + bytesWritten, count);
				secondPosition += count;
				Position2 += count;
				bytesWritten += count;
			}
		} 
	}
}