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
import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.datamodels.ScrolltextDataModel;
import kr.co.turtlelab.andowsignage.tools.CanvasUtils;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

public class ScrollTextView extends RelativeLayout {

    private final Activity act;
    private final Context ctx;
    private final TextView tv;
    private List<ScrolltextDataModel> sdmList = new ArrayList<>();
    private int blankLength = 0;

    public ScrollTextView(Activity act, Context context, int width, int height, List<ScrolltextDataModel> sdmList) {
        super(context);

        this.act = act;
        this.ctx = context;

        setGravity(Gravity.CENTER_VERTICAL);
        setMinimumWidth(width);
        setMinimumHeight(height);

        tv = new TextView(ctx);
        tv.setSingleLine();
        tv.setGravity(Gravity.CENTER_VERTICAL);

        RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
        addView(tv, params);

        bindScrollTexts(width, height, sdmList);
    }

    public void bindScrollTexts(int width, int height, List<ScrolltextDataModel> data) {
        stopPlayback();
        setMinimumWidth(width);
        setMinimumHeight(height);
        sdmList = data != null ? data : new ArrayList<ScrolltextDataModel>();
        if (sdmList.isEmpty()) {
            setVisibility(View.GONE);
            tv.setText("");
            return;
        }
        setVisibility(View.VISIBLE);
        applyStyle();
        setTextStrings();
    }

    public void startPlayback() {
        if (sdmList == null || sdmList.isEmpty() || getMinimumWidth() <= 0 || getMinimumHeight() <= 0) {
            return;
        }
        clearAnimation();
        tv.clearAnimation();
        Animation anim = buildMarqueeAnim();
        if (anim != null) {
            tv.startAnimation(anim);
        }
    }

    public void stopPlayback() {
        clearAnimation();
        tv.clearAnimation();
    }

    public void clearContent() {
        stopPlayback();
        sdmList = new ArrayList<>();
        tv.setText("");
        setVisibility(View.GONE);
    }

    private void applyStyle() {
        ScrolltextDataModel first = sdmList.get(0);
        Typeface font = resolveTypeface(first.getFontFileName());
        tv.setTypeface(font);
        setBackgroundColor(Color.parseColor(first.getBackground()));
        tv.setTextColor(Color.parseColor(first.getForeground()));
        int fontSize = resolveFontSize(font);
        tv.setTextSize(TypedValue.COMPLEX_UNIT_PX, fontSize);
        Paint textPaint = tv.getPaint();
        float blankWidth = Math.max(1f, textPaint.measureText(" "));
        blankLength = Math.max(1, (int) (Math.max(1, getMinimumWidth()) / blankWidth));
    }

    private Typeface resolveTypeface(String fontFileName) {
        Typeface font = null;
        try {
            String fontpath = LocalPathUtils.getFontFilePath(fontFileName);
            File fontfile = new File(fontpath);
            if (fontfile.exists()) {
                font = Typeface.createFromFile(fontfile);
            }
        } catch (Exception ignored) {
        }
        if (font != null) {
            return font;
        }
        return Typeface.createFromAsset(act.getAssets(), "fonts/NanumGothic.otf");
    }

    private int resolveFontSize(Typeface font) {
        int fontSize = 12;
        while (getHeight(ctx, "yY", font, fontSize, Math.max(1, getMinimumWidth())) <= getMinimumHeight()) {
            fontSize++;
        }
        return Math.max(12, fontSize - 1);
    }

    private void setTextStrings() {
        StringBuilder sb = new StringBuilder();
        boolean noFirst = false;
        for (ScrolltextDataModel sdm : sdmList) {
            if (noFirst) {
                sb.append(fill(blankLength, " "));
            }
            sb.append(sdm.getText());
            noFirst = true;
        }
        tv.setText(sb);
    }

    private Animation buildMarqueeAnim() {
        if (sdmList.isEmpty()) {
            return null;
        }
        float deltaXfrom = getMinimumWidth();
        int deltaXto = -(CanvasUtils.getActualSize(tv)[0]);

        Animation anim = new TranslateAnimation(deltaXfrom, deltaXto, 0.0f, 0.0f);
        double multiply = (double) CanvasUtils.getActualSize(tv)[0] / Math.max(1, getMinimumWidth());
        if (multiply < 1.0) {
            multiply = 1.0;
        }
        anim.setDuration((int) (multiply * sdmList.get(0).getScrolltime() * 1000));
        anim.setRepeatMode(Animation.RESTART);
        anim.setRepeatCount(Animation.INFINITE);
        anim.setInterpolator(AnimationUtils.loadInterpolator(ctx, android.R.anim.linear_interpolator));
        return anim;
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

    public String fill(int length, String with) {
        StringBuilder sb = new StringBuilder(length);
        while (sb.length() < length) {
            sb.append(with);
        }
        return sb.toString();
    }
}
