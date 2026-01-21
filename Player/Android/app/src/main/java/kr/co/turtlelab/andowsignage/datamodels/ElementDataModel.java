package kr.co.turtlelab.andowsignage.datamodels;

public class ElementDataModel {
	
	int id;
    String name = "";
    String type = "";
    int row  = 0;
    int col = 0;
    int rowSpan = 0;
    int colSpan = 0;
    int width = 0;
    int height = 0;
    int scaledWidth = 0;
    int scaledHeight = 0;
    int posX = 0;
    int posY = 0;
    int scaledPosX = 0;
    int scaledPosY = 0;
    String zorder = "0";
    
    float scaleX = 1.0f;
    float scaleY = 1.0f;
    float scale = 1.0f;
     
    public void updateScales(float scale, float scaleX, float scaleY) {
    	this.scale = scale;
    	this.scaleX = scaleX;
    	this.scaleY = scaleY;
    	this.scaledWidth = Math.round(this.width * scaleX);
    	this.scaledHeight = Math.round(this.height * scaleY);
    	this.scaledPosX = (int)Math.round(this.posX * scaleX);
    	this.scaledPosY = (int)Math.round(this.posY * scaleY);
    }
    
    /************* Define Setter Methods *********/
     
    public void setid(String id)
    {
        this.id = Integer.parseInt(id);
    }
    public void setName(String name)
    {
        this.name = name;
    }
    public void setType(String type)
    {
        this.type = type;
    }
    public void setRow(String row)
    {
        this.row = Integer.parseInt(row);
    }
    public void setCol(String col)
    {
        this.col = Integer.parseInt(col);
    }
    public void setRowspan(String rowspan)
    {
        this.rowSpan = Integer.parseInt(rowspan);
    }
    public void setColspan(String colspan)
    {
        this.colSpan = Integer.parseInt(colspan);
    }
    public void setScales(float scale, float scaleX, float scaleY) {
    	this.scale = scale;
    	this.scaleX = scaleX;
    	this.scaleY = scaleY;
    }
    public void setWidth(String width)
    {
    	double value = Double.parseDouble(width);
    	this.width = (int)Math.round(value);
        this.scaledWidth = (int)Math.round(value * scaleX);
    }
    public void setHeight(String height)
    {
    	double value = Double.parseDouble(height);
    	this.height = (int)Math.round(value);
        this.scaledHeight = (int)Math.round(value * scaleY);
    }
    public void setX(String x)
    {
    	double _val = Double.parseDouble(x);
    	this.posX = (int)_val;
        this.scaledPosX = (int)Math.round(_val * scaleX);
    }
    public void setY(String y)
    {
    	double _val = Double.parseDouble(y);
    	this.posY = (int)_val;
        this.scaledPosY = (int)Math.round(_val * scaleY);
    }
    public void setZ(String zorder)
    {
        this.zorder = zorder;
    }
     
    
    /************* Define Getter Methods *********/
     
    public int getid()
    {
        return id;
    }
    public String getName()
    {
        return name;
    }
    public String getType()
    {
    	return type;
    }
    public int getRow()
    {
    	return row;
    }
    public int getCol()
    {
    	return col;
    }
    public int getRowspan()
    {
    	return rowSpan;
    }
    public int getColspan()
    {
    	return colSpan;
    }
    public int getWidth()
    {
    	return scaledWidth;
    }
    public int getHeight()
    {
    	return scaledHeight;
    }
    public int getX()
    {
    	return scaledPosX;
    }
    public int getY()
    {
    	return scaledPosY;
    }
    public int getBaseWidth()
    {
    	return width;
    }
    public int getBaseHeight()
    {
    	return height;
    }
    public int getBaseX()
    {
    	return posX;
    }
    public int getBaseY()
    {
    	return posY;
    }
    public String getZorder()
    {
    	return zorder;
    }
}
