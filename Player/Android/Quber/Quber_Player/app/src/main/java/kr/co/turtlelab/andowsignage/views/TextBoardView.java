package kr.co.turtlelab.andowsignage.views;

import android.content.Context;
import android.widget.RelativeLayout;
import android.widget.TextView;

import java.util.List;

import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;

public class TextBoardView extends RelativeLayout {

	int i = 0;
	TextView tv;
	
	public TextBoardView(Context context, List<MediaDataModel> cdmList) {
		super(context);
		tv = new TextView(context);
		addView(tv);
	}
	
	

}
