package kr.co.turtlelab.andowsignage.views;

import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.Bitmap;
import android.net.http.SslError;
import android.os.Build;
import android.util.AttributeSet;
import android.webkit.JavascriptInterface;
import android.webkit.SslErrorHandler;
import android.webkit.WebSettings;
import android.webkit.WebSettings.PluginState;
import android.webkit.WebSettings.RenderPriority;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class TurtleWebView extends WebView {
	private static Field sConfigCallback;

	static {
		try {
			sConfigCallback = Class.forName("android.webkit.BrowserFrame")
					.getDeclaredField("sConfigCallback");
			sConfigCallback.setAccessible(true);
		} catch (Exception e) {
			// ignored
		}
 
	}
 
	public TurtleWebView(Context context) {
		super(context.getApplicationContext());
		setLayerType(WebView.LAYER_TYPE_SOFTWARE, null);
		setWebViewClient(new TurtleWebViewClient());
	}
 
	public TurtleWebView(Context context, AttributeSet attrs) {
		super(context.getApplicationContext(), attrs);
		setLayerType(WebView.LAYER_TYPE_SOFTWARE, null);
		setWebViewClient(new TurtleWebViewClient());
	}
 
	public TurtleWebView(Context context, AttributeSet attrs, int defStyle) {
		super(context.getApplicationContext(), attrs, defStyle);
		setLayerType(WebView.LAYER_TYPE_SOFTWARE, null);
		setWebViewClient(new TurtleWebViewClient());
	}

	public TurtleWebView(Context context, int width, int height) {
		super(context);
		setLayerType(WebView.LAYER_TYPE_SOFTWARE, null);
		setWebViewClient(new TurtleWebViewClient());

		setMinimumWidth(width);
		setMinimumHeight(height);

		setBrowserOptions();
	}

	public TurtleWebView(Context context, int width, int height, List<MediaDataModel> dataList) {
		super(context);
		setLayerType(WebView.LAYER_TYPE_SOFTWARE, null);
		setWebViewClient(new TurtleWebViewClient());

		setMinimumWidth(width);
		setMinimumHeight(height);

		setBrowserOptions();

		if(dataList.size() > 0) {
			String url = "http://"+ AndoWSignageApp.MANAGER_IP + "/" + dataList.get(0).getRemoteSubPath();
			this.loadUrl(url);
		}
	}
	
	@SuppressLint("JavascriptInterface")
	private void setBrowserOptions() {

		WebSettings webSettings = this.getSettings();
		webSettings.setJavaScriptEnabled(true);  
		webSettings.setPluginState(PluginState.ON);
		webSettings.setRenderPriority(RenderPriority.NORMAL);
		webSettings.setCacheMode(WebSettings.LOAD_NO_CACHE);
		webSettings.setAllowFileAccess(true); 
		webSettings.setAllowContentAccess(true); 
		webSettings.setAllowFileAccessFromFileURLs(true);
		webSettings.setDomStorageEnabled(true);
		webSettings.setDatabaseEnabled(true);
		webSettings.setLoadWithOverviewMode(true);
		webSettings.setUseWideViewPort(true);
		webSettings.setAppCacheEnabled (true);
		webSettings.setUserAgentString("Mozilla/5.0 (Linux; U; Android 2.0; en-us; Droid Build/ESD20) AppleWebKit/530.17 (KHTML, like Gecko) Version/4.0 Mobile Safari/530.17");
		webSettings.setLoadsImagesAutomatically (true);
		webSettings.setDefaultTextEncodingName("UTF-8");
		webSettings.setJavaScriptCanOpenWindowsAutomatically(true);
		webSettings.setBuiltInZoomControls(false);
		webSettings.setSupportMultipleWindows(false);
		webSettings.setNeedInitialFocus(false);
		webSettings.setSupportZoom(true);
		webSettings.setLayoutAlgorithm(WebSettings.LayoutAlgorithm.NARROW_COLUMNS);

		if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
			webSettings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
		}

		if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.JELLY_BEAN){
			webSettings.setAllowUniversalAccessFromFileURLs(true);
		}else{
			try {
				Class<?> clazz = webSettings.getClass();
				Method method = clazz.getMethod("setAllowUniversalAccessFromFileURLs", boolean.class);
				if (method != null) {
					method.invoke(webSettings, true);
				}
			} catch (NoSuchMethodException e) {
				e.printStackTrace();
			} catch (InvocationTargetException e) {
				e.printStackTrace();
			} catch (IllegalAccessException e) {
				e.printStackTrace();
			}
		}

		if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT){
			setWebContentsDebuggingEnabled(true);
		}

		this.addJavascriptInterface(new JavaScriptInterface(), "JSI");
	}
	
	public void releaseWebView() {
		this.pauseTimers();
		if (Build.VERSION.SDK_INT < 18) {
			this.clearView();
		} else {
			this.loadUrl("about:blank");
		}
		this.clearCache(true);
		this.clearHistory();
	}
 
	@Override
	public void destroy() {
		super.destroy();

		try {
			if (sConfigCallback != null)
				sConfigCallback.set(null, null);
		} catch (Exception e) {
			throw new RuntimeException(e);
		}

		releaseWebView();
	}
 
	protected static class TurtleWebViewClient extends WebViewClient {

		@Override
		public void onPageStarted(WebView view, String url, Bitmap favicon) {
			super.onPageStarted(view, url, favicon);
		}

		@Override
		public void onPageFinished(WebView view, String url) {
			super.onPageFinished(view, url);
		}

		@Override
		public void onReceivedError(WebView view, int errorCode, String description, String failingUrl) {
			super.onReceivedError(view, errorCode, description, failingUrl);
		}

		@Override
		public boolean shouldOverrideUrlLoading(WebView view, String url) {
			view.loadUrl(url);
			return true;
		}

		@Override
		public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError error) {
			handler.proceed();
		}
	}

	private class JavaScriptInterface {
		@JavascriptInterface
		public void hardReload() {
			SystemUtils.runOnUiThread(new Runnable() {
				@Override
				public void run() {
					reload();
				}
			});
		}
	}
}