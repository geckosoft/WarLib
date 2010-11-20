using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeckoSoft.WarLib
{
	public class WArchive
	{
		public struct WAREntry
		{
			public static WAREntry Zero = new WAREntry(0, 0);

			public uint EntryPosition;
			public uint EntryLength;

			public WAREntry(uint location, uint entryLength)
			{
				EntryPosition = location;
				EntryLength = entryLength;
			}

			public WAREntry(uint location)
			{
				EntryPosition = location;
				EntryLength = 0;
			}

			public bool IsValid
			{
				get { return EntryPosition != uint.MaxValue; }
			}

			public override string ToString() { return String.Format("WAREntry: {0} - {1}", EntryPosition, EntryLength); }
		}

		public string Filename { get; protected set; }

		public WArchive(string filename) { Filename = filename; }

		protected byte[] Data;
		protected BinaryReader Reader;
		public UInt32 ArchiveFormat { get; protected set; }
		public WAREntry[] Entries { get; protected set; }
		public UInt16 ArchiveType { get; protected set; }

		public bool IsDemoData
		{
			get { return (ArchiveFormat == 24); }
		}

		public void Open()
		{
			Data = File.ReadAllBytes(Filename);
			Reader = new BinaryReader(new MemoryStream(Data));

			ExtractFormat();
			ExtractEntries();
			ExtractType();
			ExtractEntryOffsets();
		}

		private void ExtractEntryOffsets()
		{
			int? LastValid = null;
			var bas = Reader.BaseStream.Position;

			for (int i = 0; i < Entries.Length; i++ )
			{
				Entries[i] = new WAREntry((uint) (Reader.ReadUInt32()));

				if (i > 0 && Entries[i].IsValid && LastValid != null)
				{
					Entries[(int) LastValid].EntryLength = Entries[i].EntryPosition - Entries[(int) LastValid].EntryPosition;
				}

				if (Entries[i].IsValid)
				{
					LastValid = i;
				}
			}

			if (Entries.Length == 0)
				return;

			Entries[Entries.Length - 1].EntryLength = (uint) (Data.Length - Entries[Entries.Length - 1].EntryPosition);
		}

		private void ExtractType()
		{
			ArchiveType = Reader.ReadUInt16();
		}

		private void ExtractEntries()
		{
			Entries = new WAREntry[Reader.ReadUInt16()];
		}

		protected void ExtractFormat()
		{
			ArchiveFormat = Reader.ReadUInt32();

			if (ArchiveFormat != 25 && ArchiveFormat != 24) // wc2 - wc2 demo
			{
				throw new Exception("Invalid format detected.");
			}
		}

		public byte[] GetEntry(uint entry)
		{
			using (var ms = new MemoryStream( Data))
			{
				ms.Seek(Entries[entry].EntryPosition, SeekOrigin.Begin);

            	using (var br = new BinaryReader(ms))
            	{
            		var uncompressedLength = br.ReadUInt32();
					var flags = uncompressedLength >> 24;
					uncompressedLength &= 0x00FFFFFF;

					switch (flags)
					{
						case 0x20: // compressed
							return Decompress(ms, uncompressedLength);

						case 0x00: // not compressed
							return br.ReadBytes((int) uncompressedLength);

						default: // unknown
							throw new Exception("Unknown flag detected. Unable to get entry.");
					}
            	}
            }
		}
		/// <summary>
		/// Convert palette
		/// </summary>
		/// <param name="pal">The palette data</param>
		/// <returns>The converted (normalised) palette</returns>
		public byte[] ConvertPalette(byte[] pal)
		{
			int i;

			for (i = 0; i < 768; ++i) {
				pal[i] <<= 2;
			}

			return pal;
		}

		private byte[] Decompress(MemoryStream ms, uint uncompressedLength)
		{
			var buf = new char[4096];
			var pos = 0;
			int bi = 0;
			
			using (var br = new BinaryReader(ms))
			using (var bw = new BinaryWriter(new MemoryStream()))
			{
				while (pos < uncompressedLength)
				{
					int i;
					int bflags;

					bflags = br.ReadByte();
					for (i = 0; i < 8; ++i)
					{
						int j;
						int o;

						if ((bflags & 1) == 0)
						{
							j = br.ReadByte();
							//*dp++ = j;
							pos++;
							bw.Write((byte)j);
							buf[bi++ & 0xFFF] = (char) j;
						}
						else
						{
							o = br.ReadUInt16();
							j = (o >> 12) + 3;
							o &= 0xFFF;

							while (j-- > 0)
							{
								char inbetween;
								buf[bi++ & 0xFFF] = inbetween = buf[o++ & 0xFFF];

								bw.Write((byte)inbetween);

								pos++;
								if (pos == uncompressedLength)
									break;
							}
						}
						if (pos == uncompressedLength)
							break;

						bflags >>= 1;
					}
				}

				return ((MemoryStream) bw.BaseStream).ToArray();
			}
		}
	}
}
