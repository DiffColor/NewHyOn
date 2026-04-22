package kr.co.turtlelab.andowsignage.tools;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileReader;
import java.nio.charset.StandardCharsets;

public class AuthUtils {

    public static boolean HasAuthKey(String keypath , String mac) {

        boolean hasKey = false;

        File _file = new File(keypath);

        if(_file.exists()) {

            FileReader fileReader = null;
            BufferedReader bufferedReader = null;

            try {
                fileReader = new FileReader(_file);
                bufferedReader = new BufferedReader(fileReader);
                String line;
                while ((line = bufferedReader.readLine()) != null) {
                    if (DecodeAuthKey(line).equalsIgnoreCase(mac.replace(":",""))) {
                        hasKey = true;
                        break;
                    }
                }

            } catch (Exception e) {
                e.printStackTrace();
            } finally {

                try {
                    if (bufferedReader != null)
                        bufferedReader.close();
                } catch (Exception exc1) {
                }

                try {
                    if (fileReader != null)
                        fileReader.close();
                } catch (Exception exc1) {
                }

            }
        }

        return hasKey;
    }

    public static boolean HasEncodedAuthKey(String keypath, String encodedKey) {

        if (encodedKey == null || encodedKey.trim().length() < 1) {
            return false;
        }

        File _file = new File(keypath);
        if (!_file.exists()) {
            return false;
        }

        FileReader fileReader = null;
        BufferedReader bufferedReader = null;

        try {
            fileReader = new FileReader(_file);
            bufferedReader = new BufferedReader(fileReader);
            String line;
            while ((line = bufferedReader.readLine()) != null) {
                if (line != null && line.trim().equalsIgnoreCase(encodedKey.trim())) {
                    return true;
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
        } finally {
            try {
                if (bufferedReader != null)
                    bufferedReader.close();
            } catch (Exception exc1) {
            }

            try {
                if (fileReader != null)
                    fileReader.close();
            } catch (Exception exc1) {
            }
        }

        return false;
    }

    public static String DecodeAuthKey(String authkey) {
        byte[] asciiBytes = ConvertStringToASCIIBytes(authkey);
        byte[] restoreBytes = MoveDiff(asciiBytes);
        String restoreStr = ConvertASCIIBytesToString(restoreBytes);
        String decodedStr = RestoreMixedStr(restoreStr);

        if (authkey.isEmpty())
            return "";

        return decodedStr;
    }

    public static byte[] ConvertStringToASCIIBytes(String mixedSourcekey)
    {
        return mixedSourcekey.getBytes(StandardCharsets.US_ASCII);
    }

    public static byte[] MoveDiff(byte[] bts)
    {
        byte[] bytearr = new byte[bts.length];

        for (int i = 0; i < bts.length; i++)
        {
            int dec = bts[i];

            if (dec >= 48 && dec <= 57)
            {
                bytearr[i] = (byte)((48 + (57 - dec)) & 0xFF);
            }
            else if (dec >= 65 && dec <= 90)
            {
                bytearr[i] = (byte)((65 + (90 - dec)) & 0xFF);
            }
            else
            {
                bytearr[i] = (byte)((97 + (122 - dec)) & 0xFF);
            }
        }

        return bytearr;
    }

    public static String ConvertASCIIBytesToString(byte[] keyBytes)
    {
        return new String(keyBytes);
    }

    public static String RestoreMixedStr(String mixedkey)
    {
        if (mixedkey.length() < 12) return "";

        String retStr = "";
        int idx = 0;

        while (retStr.length() < mixedkey.length())
        {
            retStr += mixedkey.substring(idx, idx + 2);
            retStr += mixedkey.substring(idx + 6, idx + 8);
            idx += 2;
        }

        return retStr;
    }

    public static String GetPasswd2(String macStr)
    {
        /*
         * 소스키 뒤의 4자리만 가져온다.
         * 예) 605718911F5A -> 1F5A
         */
        char[] chArr = macStr.substring((macStr.length() - 4)).toCharArray();

        String numStr = "";

        /*
         * 16진수 값을 10진수 값으로 변환한다.
         * 예) 1 F 5 A -> 1 15 5 10
         */
        for (Character ch:chArr) {
            numStr += Integer.parseInt(ch.toString(), 16);
        }

        /*
         * 다시 뒤의 4자리만 가져온다.
         * 115510 -> 5510
         */
        numStr = numStr.substring((numStr.length() - 4));

        /*
         * 숫자 스트링을 뒤집는다.
         * 5510 -> 0155
         */
        numStr = new StringBuilder(numStr).reverse().toString();

        /*
         * 앞자리 0을 제거한다.
         * 0155 -> 0155
         */
            numStr = numStr.replaceFirst("^0+(?!$)", "");

        /*
         * 곱하기2 빼기1 곱하기2
         */
        return Integer.toString (((Integer.parseInt(numStr) * 2) - 1) * 2);
    }

    public static String EncodeAuthKey(String sourcekey)
    {
        String mixedStr = MixMultipleString(sourcekey);

        if (mixedStr.isEmpty()) return mixedStr;

        byte[] mixedBytes = ConvertStringToASCIIBytes(mixedStr);
        byte[] shiftedBytes = MoveDiff(mixedBytes);
        String encodedStr = ConvertASCIIBytesToString(shiftedBytes);

        return encodedStr;
    }

    // Mix Source String : AA BB CC DD EE FF => AA CC EE BB DD FF
    public static String MixMultipleString(String sourcekey)
    {
        if (sourcekey.length() < 12) return "";

        String retStr = "";
        String xStr ="";
        int idx = 0;

        while (retStr.length() < sourcekey.length() / 2)
        {
            retStr += sourcekey.substring(idx, idx+2);
            idx += 2;
            xStr += sourcekey.substring(idx, idx+2);
            idx += 2;
        }

        return retStr + xStr;
    }
}
