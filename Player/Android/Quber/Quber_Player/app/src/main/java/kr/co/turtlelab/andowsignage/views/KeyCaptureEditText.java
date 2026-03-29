package kr.co.turtlelab.andowsignage.views;

import android.content.Context;
import android.graphics.Color;
import android.os.Build;
import android.text.InputType;
import android.util.AttributeSet;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.accessibility.AccessibilityEvent;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputConnection;
import android.widget.EditText;

import java.lang.reflect.Method;

public class KeyCaptureEditText extends EditText {

	public KeyCaptureEditText(Context context) {
		super(context);
		init();
	}

	public KeyCaptureEditText(Context context, AttributeSet attrs) {
		super(context, attrs);
		init();
	}

	public KeyCaptureEditText(Context context, AttributeSet attrs, int defStyleAttr) {
		super(context, attrs, defStyleAttr);
		init();
	}

	private void init() {
		setBackgroundColor(Color.TRANSPARENT);
		setTextColor(Color.TRANSPARENT);
		setHintTextColor(Color.TRANSPARENT);
		setHighlightColor(Color.TRANSPARENT);
		setCursorVisible(false);
		setEnabled(true);
		setLongClickable(false);
		setTextIsSelectable(false);
		setFocusable(true);
		setFocusableInTouchMode(true);
		setClickable(false);
		setLongClickable(false);
		setPadding(0, 0, 0, 0);
		setSingleLine(true);
		setContentDescription("player_input_overlay");
		setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
		setImeOptions(getImeOptions() | EditorInfo.IME_FLAG_NO_EXTRACT_UI | EditorInfo.IME_FLAG_NO_FULLSCREEN);
		disableSoftInput();
	}

	private void disableSoftInput() {
		if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
			setShowSoftInputOnFocus(false);
			return;
		}

		try {
			Method method = EditText.class.getMethod("setShowSoftInputOnFocus", boolean.class);
			method.setAccessible(true);
			method.invoke(this, false);
			return;
		} catch (Exception ignored) {
		}

		try {
			Method method = EditText.class.getMethod("setSoftInputShownOnFocus", boolean.class);
			method.setAccessible(true);
			method.invoke(this, false);
		} catch (Exception ignored) {
		}
	}

	@Override
	public boolean onCheckIsTextEditor() {
		return true;
	}

	@Override
	public InputConnection onCreateInputConnection(EditorInfo outAttrs) {
		InputConnection inputConnection = super.onCreateInputConnection(outAttrs);
		if (outAttrs != null) {
			outAttrs.imeOptions |= EditorInfo.IME_FLAG_NO_EXTRACT_UI | EditorInfo.IME_FLAG_NO_FULLSCREEN;
		}
		return inputConnection;
	}

	@Override
	public boolean isSuggestionsEnabled() {
		return false;
	}

	@Override
	public boolean dispatchPopulateAccessibilityEvent(AccessibilityEvent event) {
		if (event != null) {
			event.setClassName(EditText.class.getName());
			event.setPassword(false);
		}
		return super.dispatchPopulateAccessibilityEvent(event);
	}

	@Override
	public boolean onTouchEvent(MotionEvent event) {
		return false;
	}

	@Override
	public boolean onKeyDown(int keyCode, KeyEvent event) {
		return false;
	}

	@Override
	public boolean onKeyUp(int keyCode, KeyEvent event) {
		return false;
	}
}
