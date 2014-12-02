using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RightEdge.DataRetrieval
{
	enum HTMLTagType
	{
		Open,
		Close,
		StandAlone,
	}

	class HTMLTag
	{
		private string _name;
		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		private HTMLTagType _type;
		public HTMLTagType Type
		{
			get { return _type; }
			set { _type = value; }
		}
	}

	class HTMLParser
	{
		private HTMLTag _currentTag;
		public HTMLTag CurrentTag
		{
			get { return _currentTag; }
		}

		private StringBuilder _currentString = new StringBuilder();
		public string CurrentString
		{
			get
			{
				if (_currentTag != null)
				{
					return null;
				}
				else if (_currentString == null)
				{
					return null;
				}
				return _currentString.ToString();
			}
		}

		public bool EOF
		{
			get
			{
				return _currentTag == null && _currentTag == null;
			}
		}

		private int _ch = 0;
		private TextReader _reader;

		public HTMLParser(TextReader sr)
		{
			_reader = sr;
			Consume();
		}

		public HTMLParser(string html)
		{
			_reader = new StringReader(html);
			Consume();
		}

		private void Consume()
		{
			_ch = _reader.Read();
		}
		

		//	A token is either an HTML tag or a span of text
		//	Depending on which was read, either the CurrentTage or CurrentString properties will be non-null
		//	This method will return false if the end of the stream is reached
		public bool ReadToken()
		{
			_currentTag = null;
			_currentString.Length = 0;
			if (_ch == -1)
			{
				_currentString = null;
				return false;
			}

			if (_ch == '<')
			{
				Consume();
				//	We have a tag
				_currentTag = new HTMLTag();
				_currentTag.Type = HTMLTagType.Open;

				StringBuilder nameBuilder = new StringBuilder();

				while (_ch != -1 && !char.IsWhiteSpace((char) _ch) &&
					_ch != '>')
				{
					if (_ch == '/' && nameBuilder.Length == 0)
					{
						_currentTag.Type = HTMLTagType.Close;
					}
					nameBuilder.Append((char)_ch);
					Consume();
				}

				_currentTag.Name = nameBuilder.ToString();

				//	Ignore any attributes for now
				int quoteChar = 0;
				bool endsWithSlash = false;

				while (_ch != -1)
				{
					if (endsWithSlash)
					{
						if (!char.IsWhiteSpace((char)_ch))
						{
							endsWithSlash = false;
						}
					}
					if (quoteChar != 0)
					{
						if (_ch == quoteChar)
						{
							//	now we're out of the quote
							quoteChar = 0;
						}
					}
					else
					{
						if (_ch == '\'' || _ch == '"')
						{
							quoteChar = _ch;
						}
						else if (_ch == '/')
						{
							endsWithSlash = true;
						}
						else if (_ch == '>')
						{
							Consume();
							//	close of tag
							break;
						}
					}
					Consume();
				}

				if (endsWithSlash)
				{
					_currentTag.Type = HTMLTagType.StandAlone;
				}

			}
			else
			{
				while (_ch != '<' && _ch != -1)
				{
					_currentString.Append((char)_ch);

					Consume();
				}
			}


			return true;
		}

		public bool ScanFor(string text)
		{
			while (ReadToken())
			{
				if (CurrentString != null)
				{
					if (CurrentString.Trim() == text)
					{
						return true;
					}
				}
			}
			return false;
		}

		public bool ScanForTag(string name, HTMLTagType type)
		{
			name = name.ToLowerInvariant();
			while (ReadToken())
			{
				if (CurrentTag != null)
				{
					if (CurrentTag.Name.ToLowerInvariant() == name &&
						CurrentTag.Type == type)
					{
						return true;
					}
				}
			}
			return false;
		}

		public bool ScanForSpan()
		{
			while (ReadToken())
			{
				if (CurrentString != null && CurrentString.Trim().Length > 0)
				{
					return true;
				}
			}
			return false;
		}

	}
}
