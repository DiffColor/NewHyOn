package kr.co.turtlelab.andowsignage.tools;

import android.view.View;
import android.view.View.MeasureSpec;

public class CanvasUtils {

	public static int[] getActualSize(View view) {
		view.measure(MeasureSpec.UNSPECIFIED, MeasureSpec.UNSPECIFIED);
		return new int[] { view.getMeasuredWidth(), view.getMeasuredHeight() };
	}
	
	public static float[] getScaleFactors(int width, int height, int reqWidth, int reqHeight) {
		float scaleFactor = Math.max(reqWidth / (width*1.0f), reqHeight / (height*1.0f));
		if(scaleFactor <= 0) {
			scaleFactor = 1;
		}
		float scaleXFactor = reqWidth / (width*1.0f);
		float scaleYFactor = reqHeight / (height*1.0f);
		return new float[] { scaleFactor, scaleXFactor, scaleYFactor };
	}
	
}
