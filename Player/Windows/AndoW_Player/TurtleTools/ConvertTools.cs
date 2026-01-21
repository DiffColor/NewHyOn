//using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using System.Windows.Controls;

namespace TurtleTools
{
    public class ConvertTools
    {
        /* 번호만 추리기 위한 Regex */
        public static string ParseNumbers(string sourceStr)
        {
            return Regex.Replace(sourceStr, @"[^\d]", "");
        }

        public static bool ValidNumberString(string sourceStr)
        {
            string pstr = ParseNumbers(sourceStr);

            if (pstr.Length < 9) return false;
            else if (pstr.Length > 13) return false;

            return true;
        }

        public static string ConvertPhoneNumberString(string sourceStr)
        {
            string pstr = ParseNumbers(sourceStr);
            
            if (pstr.Length == 11)
            {
                return Regex.Replace(sourceStr, @"(\d{3})(\d{4})(\d{4})", "$1-$2-$3");
            } 
            else if (pstr.Length == 10)
            {
                return Regex.Replace(sourceStr, @"(\d{2})(\d{4})(\d{4})", "$1-$2-$3");
            }
            else if (pstr.Length == 12)
            {
                return Regex.Replace(sourceStr, @"(\d{2})(\d{2})(\d{4})(\d{4})", "(+$1) $2-$3-$4");
            }
            else if (pstr.Length == 13)
            {
                return Regex.Replace(sourceStr, @"(\d{2})(\d{3})(\d{4})(\d{4})", "(+$1) $2-$3-$4");
            }
            else
            {
                return Regex.Replace(sourceStr, @"(\d{2})(\d{3})(\d{4})", "$1-$2-$3");
            }

        }

        /* 16진수 바이트 데이터를 스트링 형식으로 변환 */
        public static string HexBytesToString(byte[] buff)
        {
            string sbinary = "";
            for (int i = 0; i < buff.Length; i++)
                sbinary += buff[i].ToString("X2"); /* hex format */
            return sbinary;
        }

        /*
         * 데이터테이블을 JSON 스트링 형식으로 변환
         */
        public static string ConvertDataTabletoJSON(DataTable dt)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            Dictionary<string, object> row;
            foreach (DataRow dr in dt.Rows)
            {
                row = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    if (string.IsNullOrEmpty(dr[col].ToString())) continue;

                    if (col.ColumnName.Equals("country"))
                        if (dr[col].ToString() == "82") continue;
                    if (col.ColumnName.Equals("delay"))
                        if (dr[col].ToString() == "0") continue;

                    row.Add(col.ColumnName, dr[col]);
                }
                rows.Add(row);
            }
            return serializer.Serialize(rows);
        }

        /*
         * 데이터사전을 Get 요청을 위해 스트링 형식으로 변환 
         */
        public static string ConvertDicToGetParamStr(Dictionary<string, object> getParameters)
        {
            string paramStr = "?";
            foreach (KeyValuePair<string, object> kv in getParameters)
            {
                paramStr += kv.Key + "=" + kv.Value + "&";
            }

            return paramStr.Remove(paramStr.Length - 1);
        }

        public static int ConvertToUInt(String input)
        {
            // Replace everything that is no a digit.
            String inputCleaned = Regex.Replace(input, "[^0-9]", "");

            int value = -1;

            // Tries to parse the int, returns false on failure.
            if (int.TryParse(inputCleaned, out value))
            {
                // The result from parsing can be safely returned.
                return value;
            }

            return -1; // Or any other default value.
        }

        public static string Number2String(int number, bool isCaps = false)
        {
            Char c = (Char)((isCaps ? 65 : 97) + (number - 1));
            return c.ToString();
        }

        public static byte[] ObjectToByteArray(object obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }


        public static DataTable ConvertToDataTable<T>(IList<T> data)
        {
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            foreach (PropertyDescriptor prop in properties)
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            foreach (T item in data)
            {
                DataRow row = table.NewRow();
                foreach (PropertyDescriptor prop in properties)
                    row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                table.Rows.Add(row);
            }
            return table;
        }

		//public static DataTable ExelSheet2Datatable(string exelpath, string sheetname)
  //      {
  //          DataTable dt = new DataTable("item");
  //          FileInfo fi = new FileInfo(exelpath);

  //          if (!fi.Exists)
  //              throw new Exception("File Not Exists");

  //          using (ExcelPackage xlPackage = new ExcelPackage(fi))
  //          {
  //              ExcelWorksheet worksheet = xlPackage.Workbook.Worksheets[sheetname];

  //              if (worksheet == null)
  //                  worksheet = xlPackage.Workbook.Worksheets.FirstOrDefault();

  //              ExcelCellAddress startCell = worksheet.Dimension.Start;
  //              ExcelCellAddress endCell = worksheet.Dimension.End;

  //              //// 첫번째 열의 컬럼값을 컬럼명으로 정한다.
  //              //for (int col = startCell.Column; col <= endCell.Column; col++)
  //              //{
  //              //    dt.Columns.Add(worksheet.Cells[startCell.Row, col].Value.ToString());
  //              //}

  //              // 컬럼수만큼 컬럼명 넣기 (특수문자는 컬럼명으로 넣을 수 없어서 바꿈)
  //              int i = 1;
  //              for (int col = startCell.Column; col <= endCell.Column; col++)
  //              {
  //                  dt.Columns.Add("data"+i);
  //                  i++;
  //              }

  //              // 데이터 채우기.
  //              for (int row = startCell.Row + 1; row <= endCell.Row; row++)
  //              {
  //                  DataRow dr = dt.NewRow();
  //                  int x = 0;
  //                  for (int col = startCell.Column; col <= endCell.Column; col++)
  //                  {
  //                      object value = worksheet.Cells[row, col].Value;
  //                      dr[x++] = (value == null) ? string.Empty:value;
  //                  }
  //                  dt.Rows.Add(dr);
  //              }
  //          }

  //          return dt;
  //      }

        public static string ConvertUTF8String(string str)
        {
            System.Text.Encoding utf8 = System.Text.Encoding.UTF8;
            
            byte[] utf8Bytes = utf8.GetBytes(str);
            
            string utf8String = "";
            foreach (byte b in utf8Bytes)
            {
                 utf8String += "%" + String.Format("{0:X}", b);
            } 

            return utf8.GetString(utf8Bytes);
        }

		public static string GetCurrencyStringOnly(string str)
        {
            return Regex.Replace(str, @"[^,.\d]", "");
        }

        public static string ConvertPriceFormatString(string str)
        {
            string retStr = string.Empty;
            try
            {
                string value = Regex.Replace(str, @"[^0-9.]", "");
                double amount = Convert.ToDouble(value);
                retStr = amount.ToString("#,#.##", CultureInfo.InvariantCulture);

                if (string.IsNullOrEmpty(retStr))
                    retStr = "0";
            }
            catch (Exception e)
            {
                retStr = "0";
            }

            return retStr;
        }

        public static string ConvertToMD5(string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            HMACMD5 myhmacMD5 = new HMACMD5(dataBytes);
            return HexBytesToString(myhmacMD5.Hash);
        }

        public static string ConvertStringToMD5(string str)
        {
            MD5 md5 = MD5.Create();
            byte[] dataBytes = Encoding.UTF8.GetBytes(str);
            byte[] hashBytes = md5.ComputeHash(dataBytes);
            return HexBytesToString(hashBytes);
        }

        public static void SelectItemByName(ComboBox combobox, string value)
        {
            IEnumerable<Object> query =
                                from Object item in combobox.Items
                                where (item.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
                                select item;

            foreach (Object obj in query)
            {
                combobox.SelectedItem = obj;
                return;
            }

            if (combobox.Items.Count > 0 && combobox.SelectedItem == null) combobox.SelectedIndex = 0;
        }
    }
}
