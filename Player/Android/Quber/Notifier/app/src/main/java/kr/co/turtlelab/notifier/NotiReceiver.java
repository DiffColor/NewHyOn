package kr.co.turtlelab.notifier;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public class NotiReceiver extends BroadcastReceiver {

    @Override
    public void onReceive(Context context, Intent intent) {
        if (context == null || intent == null) {
            return;
        }

        String action = intent.getAction();
        String message = intent.getStringExtra(FullscreenActivity.EXTRA_MESSAGE);

        if (FullscreenActivity.ACTION_SHOW_MESSAGE.equals(action)) {
            final FullscreenActivity activity = FullscreenActivity.act;
            if (activity != null) {
                activity.runOnUiThread(() -> activity.handleAction(action, message));
                return;
            }

            launchActivity(context, action, message);
            return;
        }

        final FullscreenActivity activity = FullscreenActivity.act;
        if (FullscreenActivity.ACTION_FINISH.equals(action)) {
            if (activity != null) {
                activity.runOnUiThread(() -> activity.handleAction(action, message));
                return;
            }

            launchActivity(context, action, message);
        }
    }

    private void launchActivity(Context context, String action, String message) {
        Intent launchIntent = new Intent(context, FullscreenActivity.class);
        launchIntent.setAction(action);
        launchIntent.putExtra(FullscreenActivity.EXTRA_MESSAGE, message);
        launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        launchIntent.addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP);
        context.startActivity(launchIntent);
    }
}
