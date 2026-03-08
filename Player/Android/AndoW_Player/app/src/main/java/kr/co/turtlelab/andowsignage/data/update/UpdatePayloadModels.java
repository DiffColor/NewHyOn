package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;
import android.util.Base64;

import com.google.gson.Gson;
import com.google.gson.annotations.SerializedName;

import java.nio.charset.Charset;
import java.util.ArrayList;
import java.util.List;

public final class UpdatePayloadModels {

    private UpdatePayloadModels() { }

    public static final class UpdatePayload {
        public PageListInfoClass PageList;
        public List<PageInfoClass> Pages;
        public ContractPlaylistPayload Contract;
        public ScheduleUpdatePayload Schedule;
    }

    public static final class ScheduleUpdatePayload {
        public String PlayerId = "";
        public String PlayerName = "";
        public String GeneratedAt = "";
        public List<SpecialSchedulePayload> SpecialSchedules = new ArrayList<>();
        public List<SchedulePlaylistPayload> Playlists = new ArrayList<>();
        public WeeklyPlayScheduleInfo WeeklySchedule;
    }

    public static final class SchedulePlaylistPayload {
        public String PlaylistName = "";
        public PageListInfoClass PageList;
        public List<PageInfoClass> Pages = new ArrayList<>();
        public ContractPlaylistPayload Contract;
    }

    public static final class SpecialSchedulePayload {
        public String Id = "";
        public String PageListName = "";
        public boolean DayOfWeek1;
        public boolean DayOfWeek2;
        public boolean DayOfWeek3;
        public boolean DayOfWeek4;
        public boolean DayOfWeek5;
        public boolean DayOfWeek6;
        public boolean DayOfWeek7;
        public boolean IsPeriodEnable;
        public int DisplayStartH;
        public int DisplayStartM;
        public int DisplayEndH;
        public int DisplayEndM;
        public int PeriodStartYear;
        public int PeriodStartMonth;
        public int PeriodStartDay;
        public int PeriodEndYear;
        public int PeriodEndMonth;
        public int PeriodEndDay;
    }

    public static final class ContractPlaylistPayload {
        public String PlayerId = "";
        public String PlayerName = "";
        public boolean PlayerLandscape;
        public String PlaylistId = "";
        public String PlaylistName = "";
        public List<ContractPagePayload> Pages = new ArrayList<>();
    }

    public static final class ContractPagePayload {
        public String PageId = "";
        public String PageName = "";
        public int OrderIndex;
        public int PlayHour;
        public int PlayMinute;
        public int PlaySecond;
        public int Volume;
        public boolean Landscape;
        public List<ContractElementPayload> Elements = new ArrayList<>();
    }

    public static final class ContractElementPayload {
        public String ElementId = "";
        public String PageId = "";
        public String Name = "";
        public String Type = "";
        public double Width;
        public double Height;
        public double PosTop;
        public double PosLeft;
        public int ZIndex;
        public List<ContractContentPayload> Contents = new ArrayList<>();
    }

    public static final class ContractContentPayload {
        public String Uid = "";
        public String ElementId = "";
        public String FileName = "";
        public String FileFullPath = "";
        public String ContentType = "";
        public String PlayMinute = "";
        public String PlaySecond = "";
        public boolean Valid;
        public int ScrollSpeedSec;
        public String RemoteChecksum = "";
        public long FileSize;
        public boolean FileExist;
    }

    public static final class PageListInfoClass {
        @SerializedName("id")
        public String Id;
        public String PLI_PageListName = "";
        public String PLI_CreateTimeStr = "";
        public String PLI_PageDirection = "";
        public List<String> PLI_Pages = new ArrayList<>();
    }

    public static final class PageInfoClass {
        @SerializedName("id")
        public String Id;
        public String PIC_GUID = "";
        public String PIC_PageName = "";
        public int PIC_PlaytimeHour;
        public int PIC_PlaytimeMinute;
        public int PIC_PlaytimeSecond = 10;
        public int PIC_Volume;
        public boolean PIC_IsLandscape = true;
        public int PIC_Rows = 1;
        public int PIC_Columns = 1;
        public double PIC_CanvasWidth = 1920;
        public double PIC_CanvasHeight = 1080;
        public boolean PIC_NeedGuide = true;
        public String PIC_Thumb = "";
        public List<ElementInfoClass> PIC_Elements = new ArrayList<>();
    }

    public static final class ElementInfoClass {
        public String EIF_Name = "";
        public String EIF_Type = "";
        public int EIF_RowVal;
        public int EIF_ColVal;
        public int EIF_RowSpanVal;
        public int EIF_ColSpanVal;
        public double EIF_Width;
        public double EIF_Height;
        public double EIF_PosTop;
        public double EIF_PosLeft;
        public int EIF_ZIndex;
        public String EIF_DataFileName = "";
        public String EIF_DataFileFullPath = "";
        public List<ContentsInfoClass> EIF_ContentsInfoClassList = new ArrayList<>();
    }

    public static final class ContentsInfoClass {
        public String CIF_FileName = "";
        public String CIF_FileFullPath = "";
        public String CIF_RelativePath = "";
        public String CIF_StrGUID = "";
        public String CIF_PlayMinute = "00";
        public String CIF_PlaySec = "10";
        public String CIF_ContentType = "";
        public boolean CIF_ValidTime = true;
        public boolean CIF_FileExist = true;
        public int CIF_ScrollTextSpeedSec = 10;
        public String CIF_ReservedData1 = "";
        public String CIF_ReservedData2 = "";
        public long CIF_FileSize;
        public String CIF_FileHash = "";
    }

    public static final class WeeklyPlayScheduleInfo {
        @SerializedName("id")
        public String Id = "";
        public String PlayerID = "";
        public String PlayerName = "";
        public DaySchedule MonSch;
        public DaySchedule TueSch;
        public DaySchedule WedSch;
        public DaySchedule ThuSch;
        public DaySchedule FriSch;
        public DaySchedule SatSch;
        public DaySchedule SunSch;
    }

    public static final class DaySchedule {
        public int StartHour;
        public int StartMinute;
        public int EndHour;
        public int EndMinute;
        public boolean IsOnAir = true;
    }

    public static final class UpdatePayloadCodec {
        private static final Gson GSON = new Gson();

        public static String encode(UpdatePayload payload) {
            if (payload == null) {
                return "";
            }
            String json = GSON.toJson(payload);
            if (TextUtils.isEmpty(json)) {
                return "";
            }
            try {
                byte[] data = json.getBytes(Charset.forName("UTF-8"));
                return Base64.encodeToString(data, Base64.NO_WRAP);
            } catch (Exception ignore) {
                return "";
            }
        }

        public static UpdatePayload decode(String base64) {
            if (TextUtils.isEmpty(base64)) {
                return null;
            }
            String json;
            try {
                byte[] data = Base64.decode(base64, Base64.DEFAULT);
                json = new String(data, Charset.forName("UTF-8"));
            } catch (Exception ignore) {
                json = base64;
            }
            if (TextUtils.isEmpty(json)) {
                return null;
            }
            try {
                return GSON.fromJson(json, UpdatePayload.class);
            } catch (Exception ignore) {
                return null;
            }
        }
    }
}
