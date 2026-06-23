export type DisplayType = 'None' | 'Media' | 'ScrollText' | 'WelcomeBoard';
export type ContentType = 'None' | 'Video' | 'Image';

export interface ContentsInfoClass {
  CIF_FileName: string;
  CIF_FileFullPath?: string;
  CIF_RelativePath?: string;
  CIF_StrGUID?: string;
  CIF_PlayMinute?: string;
  CIF_PlaySec?: string;
  CIF_ContentType: ContentType | string;
  CIF_ValidTime?: boolean;
  CIF_FileExist?: boolean;
  CIF_ScrollTextSpeedSec?: number;
  CIF_ReservedData1?: string;
  CIF_ReservedData2?: string;
  CIF_FileSize?: number;
  CIF_FileHash?: string;
}

export interface ElementInfoClass {
  EIF_Name?: string;
  EIF_Type: DisplayType | string;
  EIF_RowVal?: number;
  EIF_ColVal?: number;
  EIF_RowSpanVal?: number;
  EIF_ColSpanVal?: number;
  EIF_Width: number;
  EIF_Height: number;
  EIF_PosTop: number;
  EIF_PosLeft: number;
  EIF_ZIndex?: number;
  EIF_DataFileName?: string;
  EIF_DataFileFullPath?: string;
  EIF_IsMuted?: boolean;
  EIF_ContentsInfoClassList?: ContentsInfoClass[];
}

export interface PageInfoClass {
  PIC_GUID?: string;
  PIC_PageName?: string;
  PIC_PlaytimeHour?: number;
  PIC_PlaytimeMinute?: number;
  PIC_PlaytimeSecond?: number;
  PIC_Volume?: number;
  PIC_IsLandscape?: boolean;
  PIC_Rows?: number;
  PIC_Columns?: number;
  PIC_CanvasWidth?: number;
  PIC_CanvasHeight?: number;
  PIC_NeedGuide?: boolean;
  PIC_Thumb?: string;
  PIC_Elements?: ElementInfoClass[];
}

export interface PlayerManifest {
  playlistName: string;
  preserveAspectRatio: boolean;
  pages: PageInfoClass[];
}
