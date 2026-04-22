package kr.co.turtlelab.andowsignage.tools;

import android.annotation.SuppressLint;
import android.content.res.Resources;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Debug;
import android.view.View;

import com.nostra13.universalimageloader.core.ImageLoader;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;

public class ImageUtils {

	public static int[] getResolutionFromPath(String imagePath) {
		BitmapFactory.Options options = new BitmapFactory.Options();
		options.inJustDecodeBounds = true;
		BitmapFactory.decodeFile(imagePath, options);
		
		return new int[] { options.outWidth, options.outHeight };
	}
	
	public static Bitmap getResizedBitmap(String imagePath, int width, int height, int reqWidth, int reqHeight) {
		
		BitmapFactory.Options options = new BitmapFactory.Options();
		options.inJustDecodeBounds = true;
		
		// Determine how much to scale down the image     
		int scaleFactor = Math.max(width / reqWidth, height / reqHeight);
				
		if(scaleFactor < 1) {
			scaleFactor = 1;
		}
		
		// Decode the image file into a Bitmap sized to fill the View     
		options.inSampleSize = scaleFactor;   
		
		Bitmap bm = BitmapFactory.decodeFile(imagePath, options);

		Bitmap retBmp = Bitmap.createScaledBitmap(bm, reqWidth, reqHeight, true);
		bm.recycle();
		
		if(scaleFactor == 1) {
			return retBmp;
		}
		
		System.gc();
		return retBmp;
	}
	
	public static void releaseBmpMemory(View view) {
		try {

			Method m = View.class.getDeclaredMethod("clearDisplayList");
			m.setAccessible(true);
		 	m.invoke(view);
		} catch (NoSuchMethodException e) {
			e.printStackTrace();
		} catch (InvocationTargetException e) {
			e.printStackTrace();
		} catch (IllegalAccessException e) {
			e.printStackTrace();
		}
	}
	
	
    public int calculateInSampleSize(BitmapFactory.Options options, int reqW, int reqH) {

          int imageHeight = options.outHeight;
          int imageWidth = options.outWidth;
          int inSampleSize = 1;
          if (imageHeight > reqH || imageWidth > reqW) {
              int heightRatio = Math.round((float) imageHeight / (float) reqH);
              int widthRatio = Math.round((float) imageWidth / (float) reqW);
              inSampleSize = heightRatio < widthRatio ? heightRatio : widthRatio;
              System.out.println("i if-satsen!");
              System.out.println("height-ratio: " + heightRatio + "\nwidth-ratio: " + widthRatio);
          }
          System.out.println("samplesize: " +  inSampleSize);
//          inSampleSize = inSampleSize;

          return inSampleSize;
      }

      @SuppressLint("NewApi")
      public Bitmap[] decodeSampledBitmapFromResource(Resources res, int[] resId, int[] reqW, int[] reqH) {

          Bitmap[] scaledBitmap = new Bitmap[resId.length];
          BitmapFactory.Options options;
          for (int i = 0; i < resId.length; i++) {
              options = new BitmapFactory.Options();
              options.inJustDecodeBounds = true;
              BitmapFactory.decodeResource(res, resId[i], options);

              System.out.println("h = " + options.outHeight + " w = " + options.outWidth);
              options.inSampleSize = calculateInSampleSize(options, reqW[i], reqH[i]);

              while (options.outHeight < reqH[i] || options.outWidth < reqW[i]) {

                  options.inSampleSize--;
                  System.out.println("inSamleSize =" + options.inSampleSize);
              }

              options.inJustDecodeBounds = false;

              Bitmap bm = BitmapFactory.decodeResource(res, resId[i], options);
              System.out.println("innan omskalning: h = " + options.outHeight + " w = " + options.outWidth);
              System.out.println("antalet bytes: " + bm.getByteCount());
              System.out.println("native free size: " + Debug.getNativeHeapFreeSize() );

              scaledBitmap[i] = Bitmap.createScaledBitmap(bm, reqW[i], reqH[i], true); 
              bm.recycle();
          }
          System.gc();
          return scaledBitmap;
      }

      public static void cleanDiskcache() {
          ImageLoader.getInstance().clearDiskCache();
      }
	}
