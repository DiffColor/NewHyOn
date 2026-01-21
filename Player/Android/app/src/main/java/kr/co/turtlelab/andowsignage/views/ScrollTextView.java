package kr.co.turtlelab.andowsignage.views;

import android.app.Activity;
import android.content.Context;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.animation.Animation;
import android.view.animation.AnimationUtils;
import android.view.animation.TranslateAnimation;
import android.widget.RelativeLayout;
import android.widget.TextView;

import java.io.File;
import java.util.List;

import kr.co.turtlelab.andowsignage.datamodels.ScrolltextDataModel;
import kr.co.turtlelab.andowsignage.tools.CanvasUtils;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

public class ScrollTextView extends RelativeLayout {
	
	List<ScrolltextDataModel> sdmList;
	
	Activity act;
	Context ctx;
	
	TextView tv;
	//int height = 0;

	int blankLength = 0;
	
	public ScrollTextView(Activity act, Context context, int width, int height, List<ScrolltextDataModel> sdmList) {
		super(context);
				
		this.sdmList = sdmList;
		ctx = context;
		this.act = act;
		   		
		setMinimumWidth(width);
		//setMinimumHeight(height-height/3);
		setMinimumHeight(height);
		//this.height = height;
		
		initChildViews();
		
		setMarqueeAnim();
	}


	@Override
	protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
		//super.onMeasure(widthMeasureSpec, heightMeasureSpec);
        final int widthSize = MeasureSpec.getSize(widthMeasureSpec);
        final int heightSize = MeasureSpec.getSize(heightMeasureSpec);

        int layoutHeight = heightSize - getPaddingTop() - getPaddingBottom();

        final int childCount = getChildCount();

        for (int i = 0; i < childCount; i++) {
            final View child = getChildAt(i);

            if(child instanceof TextView) {
            	child.measure(MeasureSpec.makeMeasureSpec(child.getMeasuredWidth(), MeasureSpec.UNSPECIFIED),
            					MeasureSpec.makeMeasureSpec(child.getMeasuredHeight(), MeasureSpec.UNSPECIFIED));
            	continue;
            }

            final LayoutParams lp = (LayoutParams) child.getLayoutParams();

            int height = layoutHeight;
            int childWidthSpec;
            if (lp.width == LayoutParams.WRAP_CONTENT) {
                childWidthSpec = MeasureSpec.makeMeasureSpec(widthSize, MeasureSpec.AT_MOST);
            } else if (lp.width == LayoutParams.MATCH_PARENT) {
                childWidthSpec = MeasureSpec.makeMeasureSpec(widthSize, MeasureSpec.EXACTLY);
            } else {
                childWidthSpec = MeasureSpec.makeMeasureSpec(lp.width, MeasureSpec.EXACTLY);
            }

            int childHeightSpec;
            if (lp.height == LayoutParams.WRAP_CONTENT) {
                childHeightSpec = MeasureSpec.makeMeasureSpec(height, MeasureSpec.AT_MOST);
            } else if (lp.height == LayoutParams.MATCH_PARENT) {
                childHeightSpec = MeasureSpec.makeMeasureSpec(height, MeasureSpec.EXACTLY);
            } else {
                childHeightSpec = MeasureSpec.makeMeasureSpec(lp.height, MeasureSpec.EXACTLY);
            }

            child.measure(childWidthSpec, childHeightSpec);
        }

        setMeasuredDimension(widthSize, heightSize);
	}


	@Override
	protected void onLayout(boolean changed, int l, int t, int r, int b) {		
		final int paddingLeft = getPaddingLeft();
        final int paddingTop = getPaddingTop();

        final int childCount = getChildCount();
        int yStart = paddingTop;

        for (int i = 0; i < childCount; i++) {
            final View child = getChildAt(i);

            if (child.getVisibility() == GONE) {
                continue;
            }

            int childHeight = child.getMeasuredHeight();

            final int childTop = yStart;
            final int childBottom = childTop + childHeight;
            final int childLeft = paddingLeft;
            final int childRight = childLeft + child.getMeasuredWidth();
            child.layout(childLeft, childTop, childRight, childBottom);
        }
	}
	
	private void initChildViews() {

		if(sdmList.isEmpty()) return;

		setGravity(Gravity.CENTER_VERTICAL);
		
		tv = new TextView(ctx);

		Typeface font = null;
		
		try {
			String fontpath = LocalPathUtils.getFontFilePath(sdmList.get(0).getFontFileName());
			File fontfile = new File(fontpath);
			font = Typeface.createFromFile(fontfile);
		} catch(Exception e) {
			e.printStackTrace();
		} finally {
			if(font == null) {
				font = Typeface.createFromAsset(act.getAssets(),"fonts/NanumGothic.otf");
			}
		}

//		boolean isBold = sdmList.get(0).isBold();
//		boolean isItalic = sdmList.get(0).isItalic();
//		int style = Typeface.NORMAL;
		
//		if(isBold && isItalic) {
//			style = Typeface.BOLD_ITALIC;
//		} else if(isBold) {
//			style = Typeface.BOLD;
//		} else if(isItalic) {
//			style = Typeface.ITALIC;
//		}
		
		tv.setTypeface(font);
		
		this.setBackgroundColor(Color.parseColor(sdmList.get(0).getBackground()));
		tv.setTextColor(Color.parseColor(sdmList.get(0).getForeground()));

		//tv.setTextSize(TypedValue.COMPLEX_UNIT_PX, getMinimumHeight());

		int _fsize = 12;
		boolean finding = true;
		while (finding) {
			if(getHeight(ctx, "yY", font, _fsize, this.getMinimumWidth()) > getMinimumHeight())
				finding = false;
			else
				_fsize++;
		}
				
		Paint textPaint = tv.getPaint();
		float blankwidth = textPaint.measureText(" ");
		blankLength = (int)(getMinimumWidth() / blankwidth);

//		int _fsize = 12;
		tv.setTextSize(TypedValue.COMPLEX_UNIT_PX, _fsize);
//		boolean finding = true;
//		while(finding) {
//			Rect result = new Rect();
//			textPaint.getTextBounds("yY", 0, 2, result);
//			if(result.height() > getMinimumHeight())
//				finding = false;
//			_fsize++;
//			tv.setTextSize(TypedValue.COMPLEX_UNIT_PX, _fsize);
//		}

		tv.setSingleLine();
		tv.setGravity(Gravity.CENTER_VERTICAL);
		//tv.setTextAlignment(View.TEXT_ALIGNMENT_GRAVITY);

		RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
		addView(tv, params);
	}

	public static int getHeight(Context context, String text, Typeface font, int textSize, int deviceWidth) {
		TextView textView = new TextView(context);
		textView.setTypeface(font);
		textView.setTextSize(TypedValue.COMPLEX_UNIT_PX, textSize);
		textView.setText(text);
		int widthMeasureSpec = MeasureSpec.makeMeasureSpec(deviceWidth, MeasureSpec.UNSPECIFIED);
		int heightMeasureSpec = MeasureSpec.makeMeasureSpec(0, MeasureSpec.UNSPECIFIED);
		textView.measure(widthMeasureSpec, heightMeasureSpec);
		return textView.getMeasuredHeight();
	}

	private void setMarqueeAnim() {		
		
		setTextStrings(sdmList);
		
		float deltaXfrom = getMinimumWidth();
		//float deltaXfrom = CanvasUtils.getActualSize(tv)[0];
		int deltaXto = -(CanvasUtils.getActualSize(tv)[0]);
		
		Animation anim = new TranslateAnimation(deltaXfrom, deltaXto, 0.0f, 0.0f);
		
		//anim.setDuration(getMinimumWidth() * AndoWSignageApp.SCROLL_SPEED_RATIO);
		//anim.setDuration(CanvasUtils.getActualSize(tv)[0] / sdmList.get(0).getScrolltime() * 100);
		
		double multiply = (double)CanvasUtils.getActualSize(tv)[0] / getMinimumWidth();
		if(multiply < 1.0) multiply = 1.0;
		anim.setDuration((int)(multiply * sdmList.get(0).getScrolltime()* 1000));
		anim.setRepeatMode(Animation.RESTART);
//		anim.setFillEnabled(true);
		//anim.setFillAfter(true);
		anim.setRepeatCount(Animation.INFINITE);
		anim.setInterpolator(AnimationUtils.loadInterpolator(ctx, android.R.anim.linear_interpolator));
//		anim.setAnimationListener(new AnimationListener() {
//			
//			@Override
//			public void onAnimationStart(Animation animation) {
//			}
//			
//			@Override
//			public void onAnimationRepeat(Animation animation) {
//			}
//			
//			@Override
//			public void onAnimationEnd(Animation animation) {
//				setTextString();
////				animation.reset();
////				animation.startNow();
//				clearMarqueeAnim();
//				setMarqueeAnim();
//			}
//		});
		 
		 tv.startAnimation(anim);
	}
	
//	public void setNextMarquee() {
//		setMarqueeAnim();
//	}
//	
//	public void clearMarqueeAnim() {
//		tv.clearAnimation();
//		removeView(tv);
//	}

	private void setTextStrings(List<ScrolltextDataModel> sdmList) {
		StringBuilder sb = new StringBuilder();
		
		boolean noFirst = false;
		for (ScrolltextDataModel sdm : sdmList) {
			if(noFirst) {
				sb.append(fill(blankLength, " "));
			}
				
			sb.append(sdm.getText());
			noFirst = true;
				
		}
		tv.setText(sb);
	}
	
//	int idx = 0;
//	private void setTextString() {
//		if(idx > sdmList.size() -1) idx = 0;
//		
//		StringBuilder str = new StringBuilder();
//		str.append(sdmList.get(idx).getText());
//		str.append(" ");
//		tv.setText(str);
//		
//		idx++;
//	}
	
	public String fill(int length, String with) {
	    StringBuilder sb = new StringBuilder(length);
	    while (sb.length() < length) {
	        sb.append(with);
	    }
	    return sb.toString();
	}
	
//	public String fill(String value, int length, String with) {
//
//	    StringBuilder result = new StringBuilder(length);
//	    result.append(value);
//	    result.append(fill(Math.max(0, length - value.length()), with));
//
//	    return result.toString();
//
//	}
//
//	public String pad(String value, int length) {
//	    return pad(value, length, " ");
//	}
//
//	public String pad(String value, int length, String with) {
//	    StringBuilder result = new StringBuilder(length);
//	    result.append(fill(Math.max(0, length - value.length()), with));
//	    result.append(value);
//
//	    return result.toString();
//	}	
}
