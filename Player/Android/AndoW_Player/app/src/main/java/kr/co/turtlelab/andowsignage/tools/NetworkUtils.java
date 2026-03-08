package kr.co.turtlelab.andowsignage.tools;

import android.app.Activity;
import android.content.Context;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.net.wifi.WifiInfo;
import android.net.wifi.WifiManager;

import java.io.BufferedReader;
import java.io.FileReader;
import java.io.IOException;
import java.net.NetworkInterface;
import java.util.Collections;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;

public class NetworkUtils {
    public static int TYPE_NOT_CONNECTED = -1;
    public static int TYPE_MOBILE = 0;
	public static int TYPE_WIFI = 1;
    public static int TYPE_ETHERNET = 9;
     
     
    public static int getConnectivityStatus(Context context) {
        ConnectivityManager cm = (ConnectivityManager) context
                .getSystemService(Context.CONNECTIVITY_SERVICE);
 
        NetworkInfo activeNetwork = cm.getActiveNetworkInfo();
        if (null != activeNetwork) {

            if(activeNetwork.getType() == ConnectivityManager.TYPE_MOBILE)
                return TYPE_MOBILE;
            
            if(activeNetwork.getType() == ConnectivityManager.TYPE_WIFI)
                return TYPE_WIFI;
             
            if(activeNetwork.getType() == ConnectivityManager.TYPE_ETHERNET)
                return TYPE_ETHERNET;
        } 
        return TYPE_NOT_CONNECTED;
    }
     
    public static String getConnectivityStatusString(Context context) {
        int conn = getConnectivityStatus(context);
        String status = null;
        if (conn == TYPE_MOBILE) {
            status = "Mobile data enabled";
        } else if (conn == TYPE_WIFI) {
            status = "Wifi enabled";
        } else if (conn == TYPE_ETHERNET)  {
        	status = "Ethernet enabled";
        } else {
            status = "Not connected to Internet";
        }
        return status;
    }

	public static String getMACAddress() {
        String macAddress = "";

        try {
            if (AndoWSignageApp.networkState == TYPE_WIFI) {
                macAddress = getMACAddressByWifiPath();
                if(macAddress.length() < 1)
                    macAddress = getMACAddressByInterfaceName("wlan0");
                if(macAddress.length() < 1)
                    macAddress = getMACAddressByInterfaceName("wlan1");
            } else {
                macAddress = getMACAddressByEthernetPath();
                if(macAddress.length() < 1)
                    macAddress = getMACAddressByInterfaceName("eth0");
                if(macAddress.length() < 1)
                    macAddress = getMACAddressByInterfaceName("eth1");
            }
        } catch (Exception ee) {}

        return macAddress;
    }

    public static String getMACAddressByInterfaceName(String interfaceName) {
        try {
            List<NetworkInterface> interfaces = Collections.list(NetworkInterface.getNetworkInterfaces());
            for (NetworkInterface intf : interfaces) {
                if (interfaceName != null) {
                    if (!intf.getName().equalsIgnoreCase(interfaceName)) continue;
                }
                byte[] mac = intf.getHardwareAddress();
                if (mac==null) return "";
                StringBuilder buf = new StringBuilder();
                for (int idx=0; idx<mac.length; idx++)
                    buf.append(String.format("%02X:", mac[idx]));
                if (buf.length()>0) buf.deleteCharAt(buf.length()-1);
                return buf.toString();
            }
        } catch (Exception ex) { } // for now eat exceptions
        return "";
    }

    /*
     * Load file content to String
     */
    public static String loadFileAsString(String filePath) {
        try {
            StringBuffer fileData = new StringBuffer(1000);
            BufferedReader reader = new BufferedReader(new FileReader(filePath));
            char[] buf = new char[1024];
            int numRead = 0;
            while ((numRead = reader.read(buf)) != -1) {
                String readData = String.valueOf(buf, 0, numRead);
                fileData.append(readData);
            }
            reader.close();
            return fileData.toString();
        } catch (IOException ioe) {
            return "";
        }
    }

    /*
     * Get the STB MacAddress
     */
    public static String getMACAddressByEthernetPath(){
        String macAddress = "";
        macAddress = safeLoadMac("/sys/class/net/eth0/address");
        if (macAddress.isEmpty()) {
            macAddress = safeLoadMac("/sys/class/net/eth1/address");
        }
        return macAddress;
    }

    public static String getMACAddressByWifiPath(){
        String macAddress = safeLoadMac("/sys/class/net/wlan0/address");
        if (macAddress.isEmpty()) {
            macAddress = safeLoadMac("/sys/class/net/wlan1/address");
        }
        return macAddress;
    }

    private static String safeLoadMac(String path) {
        try {
            String raw = loadFileAsString(path).toUpperCase();
            if (raw.length() >= 17) {
                return raw.substring(0, 17);
            }
        } catch (Exception ignored) {
        }
        return "";
    }
	
//	public static String getIPAddress(Activity activity) {
//		String ip = "127.0.0.1";
//	    try {
//			if(AndoWSignageApp.networkState == NetworkUtils.TYPE_WIFI) {
//				WifiManager wm = (WifiManager) activity.getSystemService(Context.WIFI_SERVICE);
//				int ipAddress = wm.getConnectionInfo().getIpAddress();
//				ip = String.format("%d.%d.%d.%d", (ipAddress & 0xff),(ipAddress >> 8 & 0xff),(ipAddress >> 16 & 0xff),(ipAddress >> 24 & 0xff));
//			} else if(AndoWSignageApp.networkState == NetworkUtils.TYPE_ETHERNET) {
//				Enumeration<NetworkInterface> nwis;
//			        nwis = NetworkInterface.getNetworkInterfaces();
//			        while (nwis.hasMoreElements()) {
//
//			            NetworkInterface ni = nwis.nextElement();
//			            for (InterfaceAddress ia : ni.getInterfaceAddresses())
//			            	if(!ia.getAddress().isLoopbackAddress()) {
//			            		ip = ia.getAddress().toString().split("/")[1];
//			            	}
//			        }
//			}
//	    }catch (Exception e) {
//	        e.printStackTrace();
//	    }
//		return ip;
//	}
	
	public static String convertArrToIPFormat(String[] ipArr) {
        if(ipArr[0].isEmpty() || ipArr[1].isEmpty() || ipArr[2].isEmpty() || ipArr[3].isEmpty())
            return "127.0.0.1";

		return String.format("%s.%s.%s.%s", ipArr[0], ipArr[1], ipArr[2], ipArr[3]);
	}
	
	public static String convertArrToTcpStr(String[] ipArr, int port) {
		String ip = convertArrToIPFormat(ipArr);
		return String.format("tcp://%s:%d", ip, port);
	}
	
	public static String convertIPStrToTcpStr(String ipStr, int port) {
        if(ipStr.isEmpty())
            ipStr = "127.0.0.1";

		return String.format("tcp://%s:%d", ipStr, port);
	}
	
	public static boolean isIPAddr(String ipStr) {	
		String[] _iparr = ipStr.replaceAll("[^0-9 ]", "").split(".");
		return _iparr.length == 4;
	}
}
