package kr.co.turtlelab.andowsignage.views;

import android.view.ViewGroup;
import android.widget.ImageView;

import com.nostra13.universalimageloader.core.imageaware.ImageViewAware;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;

public class SafeImageViewAware extends ImageViewAware {

    public SafeImageViewAware(ImageView imageView) {
        super(imageView);
    }

    public SafeImageViewAware(ImageView imageView, boolean checkActualViewSize) {
        super(imageView, checkActualViewSize);
    }

    @Override
    public int getWidth() {
        ImageView view = getWrappedView();
        if (view == null) {
            return 0;
        }
        int width = view.getWidth();
        if (width <= 0) {
            width = view.getMeasuredWidth();
        }
        if (width <= 0) {
            ViewGroup.LayoutParams params = view.getLayoutParams();
            if (params != null && params.width > 0) {
                width = params.width;
            }
        }
        if (width <= 0) {
            width = AndoWSignageApp.getDeviceWidth();
        }
        return width;
    }

    @Override
    public int getHeight() {
        ImageView view = getWrappedView();
        if (view == null) {
            return 0;
        }
        int height = view.getHeight();
        if (height <= 0) {
            height = view.getMeasuredHeight();
        }
        if (height <= 0) {
            ViewGroup.LayoutParams params = view.getLayoutParams();
            if (params != null && params.height > 0) {
                height = params.height;
            }
        }
        if (height <= 0) {
            height = AndoWSignageApp.getDeviceHeight();
        }
        return height;
    }
}
