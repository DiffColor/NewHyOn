package kr.co.turtlelab.andowsignage.views;

import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap.Config;
import android.net.Uri;
import android.text.TextUtils;
import android.view.View;
import android.widget.ImageView;
import android.widget.ImageView.ScaleType;
import android.widget.RelativeLayout;

import com.nostra13.universalimageloader.core.DisplayImageOptions;
import com.nostra13.universalimageloader.core.ImageLoader;
import com.nostra13.universalimageloader.core.assist.ImageScaleType;
import com.nostra13.universalimageloader.core.assist.FailReason;
import com.nostra13.universalimageloader.core.listener.SimpleImageLoadingListener;

import java.io.File;

import kr.co.turtlelab.andowsignage.datamodels.WelcomeDataModel;

public class WelcomeView extends RelativeLayout {

    private WelcomeDataModel wdm;
    private final ImageView imgView;
    private final DisplayImageOptions imgOpt;

    public WelcomeView(Activity act, Context context, int width, int height, WelcomeDataModel wdm) {
        super(context);

        setMinimumWidth(width);
        setMinimumHeight(height);

        RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
                RelativeLayout.LayoutParams.MATCH_PARENT,
                RelativeLayout.LayoutParams.MATCH_PARENT);

        imgView = new ImageView(context);
        imgView.setScaleType(ScaleType.FIT_XY);
        imgView.setLayerType(View.LAYER_TYPE_SOFTWARE, null);
        imgView.setVisibility(View.GONE);

        addView(imgView, params);

        imgOpt = new DisplayImageOptions.Builder()
                .bitmapConfig(Config.RGB_565)
                .cacheInMemory(false)
                .cacheOnDisk(true)
                .resetViewBeforeLoading(true)
                .imageScaleType(ImageScaleType.EXACTLY)
                .build();

        bindWelcomeData(width, height, wdm);
    }

    public void bindWelcomeData(int width, int height, WelcomeDataModel data) {
        setMinimumWidth(width);
        setMinimumHeight(height);
        this.wdm = data;
        prepareContent(null);
    }

    public void clearContent() {
        wdm = null;
        imgView.setImageDrawable(null);
        imgView.setVisibility(View.GONE);
        setVisibility(View.GONE);
    }

    public void prepareContent(final Runnable onPrepared) {
        if (wdm == null || TextUtils.isEmpty(wdm.getLocalImgPath())) {
            clearContent();
            if (onPrepared != null) {
                onPrepared.run();
            }
            return;
        }
        File file = new File(wdm.getLocalImgPath());
        if (!file.exists()) {
            clearContent();
            if (onPrepared != null) {
                onPrepared.run();
            }
            return;
        }
        setVisibility(View.VISIBLE);
        imgView.setVisibility(View.VISIBLE);
        String uri = Uri.fromFile(file).toString();
        ImageLoader.getInstance().displayImage(Uri.decode(uri), new SafeImageViewAware(imgView), imgOpt, new SimpleImageLoadingListener() {
            @Override
            public void onLoadingComplete(String imageUri, View view, android.graphics.Bitmap loadedImage) {
                if (onPrepared != null) {
                    onPrepared.run();
                }
            }

            @Override
            public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                if (onPrepared != null) {
                    onPrepared.run();
                }
            }
        });
    }
}
