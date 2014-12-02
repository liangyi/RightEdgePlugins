using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlTypes;

namespace RightEdge.DataStorage
{
	class SqlDateUtil
	{
		//	SQL DateTime rounding code adapted from: http://stackoverflow.com/questions/2872444/round-net-datetime-milliseconds-so-it-can-fit-sql-server-milliseconds
		//  milliseconds modulo 10:                         0   1   2   3   4   5   6   7   8   9
		private static readonly int[] ROUND_DOWN_OFFSET = { 0, -1, -2, 0, -1, -2, -3, 0, -1, -2 };
		private static readonly int[] ROUND_UP_OFFSET = { 0, 2, 1, 0, 3, 2, 1, 0, 2, 1 };

		private static readonly DateTime MinSQLDate = System.Data.SqlTypes.SqlDateTime.MinValue.Value;
		private static readonly DateTime MaxSQLDate = System.Data.SqlTypes.SqlDateTime.MaxValue.Value;

		public static SqlDateTime MakeValidSQLDate(DateTime date, bool roundUp)
		{
			DateTime msDate = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, date.Millisecond);
			if (roundUp && date != DateTime.MaxValue && msDate < date)
			{
				msDate = msDate.AddMilliseconds(1);
			}

			if (msDate < MinSQLDate)
			{
				return MinSQLDate;
			}
			else if (msDate > MaxSQLDate)
			{
				return MaxSQLDate;
			}

			int milliseconds = msDate.Millisecond;
			int t = milliseconds % 10;
			int offset = roundUp ? ROUND_UP_OFFSET[t] : ROUND_DOWN_OFFSET[t];
			DateTime rounded = msDate.AddMilliseconds(offset);

			if (rounded < MinSQLDate)
			{
				return MinSQLDate;
			}
			else if (rounded > MaxSQLDate)
			{
				return MaxSQLDate;
			}

			return new SqlDateTime(rounded);
		}
	}
}
