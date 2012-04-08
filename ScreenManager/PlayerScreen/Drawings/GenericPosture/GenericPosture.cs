﻿#region License
/*
Copyright © Joan Charmant 2012.
joan.charmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.

*/
#endregion
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml;

using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Support class for custom drawings.
    /// The class takes the drawing shape and behavior from an XML file.
    /// </summary>
    public class GenericPosture
    {
        #region Properties
        public List<PointF> Points { get; private set; }
        public List<GenericPostureSegment> Segments { get; private set;}
        public List<GenericPostureEllipse> Ellipses { get; private set;}
        public List<GenericPostureAngle> Angles { get; private set;}
        public List<GenericPostureHandle> Handles { get; private set; }
        public List<GenericPostureAbstractHitZone> HitZones { get; private set;}
        public string Name { get; private set;}
        public Bitmap Icon { get; private set;}
        #endregion
        
        #region Members
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion
        
        #region Constructor
        public GenericPosture(string descriptionFile, bool info)
        {
            Points = new List<PointF>();
            Segments = new List<GenericPostureSegment>();
            Ellipses = new List<GenericPostureEllipse>();
            Handles = new List<GenericPostureHandle>();
            Angles = new List<GenericPostureAngle>();
            HitZones = new List<GenericPostureAbstractHitZone>();
            
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreWhitespace = true;
            settings.CloseInput = true;

            XmlReader reader = XmlReader.Create(descriptionFile, settings);
            if(info)
                ReadInfoXml(reader);
            else
                ReadXml(reader);
            
            reader.Close();
        }
        #endregion
        
        #region Serialization - Reading
        private void ReadInfoXml(XmlReader r)
        {
            try
            {
                r.MoveToContent();
                
                if(!(r.Name == "KinoveaPostureTool"))
            	    return;
                
            	r.ReadStartElement();
            	r.ReadElementContentAsString("FormatVersion", "");
            	
            	while(r.NodeType == XmlNodeType.Element)
    			{
                    switch(r.Name)
    				{
                        case "Name":
                            Name = r.ReadElementContentAsString();
                            break;
                        case "Icon":
                            ParseIcon(r);
                            break;
                        default:
    						r.ReadOuterXml();
    						break;
                    }
                }
                
                r.ReadEndElement();
            }
            catch(Exception e)
            {
                log.ErrorFormat("An error occurred during the parsing of a custom tool.");
                log.ErrorFormat(e.ToString());
            }
        }
        private void ReadXml(XmlReader r)
        {
            try
            {
                r.MoveToContent();
                
                if(!(r.Name == "KinoveaPostureTool"))
            	    return;
                
            	r.ReadStartElement();
            	r.ReadElementContentAsString("FormatVersion", "");
            	
            	while(r.NodeType == XmlNodeType.Element)
    			{
                    switch(r.Name)
    				{
                        case "Name":
                        case "Icon":
                             r.ReadOuterXml();
                            break;
                        case "PointCount":
                            ParsePointCount(r);
    						break;
                        case "Segments":
    						ParseSegments(r);
    						break;
    					case "Ellipses":
    						ParseEllipses(r);
    						break;
    					case "Angles":
    						ParseAngles(r);
    						break;
    					case "Handles":
    						ParseHandles(r);
    						break;
                        case "HitZone":
    						ParseHitZone(r);
    						break;
    					case "InitialConfiguration":
    						ParseInitialConfiguration(r);
    						break;
                        default:
    						string unparsed = r.ReadOuterXml();
    						log.DebugFormat("Unparsed content in XML: {0}", unparsed);
    						break;
                    }
                }
                
                r.ReadEndElement();
            }
            catch(Exception e)
            {
                log.ErrorFormat("An error occurred during the parsing of a custom tool.");
                log.ErrorFormat(e.ToString());
            }
        }
        private void ParseIcon(XmlReader r)
        {
            string base64 = r.ReadElementContentAsString();
            byte[] bytes = Convert.FromBase64String(base64);
            Icon = (Bitmap)Image.FromStream(new MemoryStream(bytes));
        }
        private void ParsePointCount(XmlReader r)
        {
            int pointCount = r.ReadElementContentAsInt();
            for(int i=0;i<pointCount;i++)
                Points.Add(Point.Empty);
        }
        private void ParseSegments(XmlReader r)
        {
            r.ReadStartElement();
            
            while(r.NodeType == XmlNodeType.Element)
            {
                if(r.Name == "Segment")
                {
                    Segments.Add(new GenericPostureSegment(r));
                }
                else
                {
                    string outerXml = r.ReadOuterXml();
                    log.DebugFormat("Unparsed content in XML: {0}", outerXml);
                }
            }
            
            r.ReadEndElement();
        }
        private void ParseEllipses(XmlReader r)
        {
            r.ReadStartElement();
            
            while(r.NodeType == XmlNodeType.Element)
            {
                if(r.Name == "Ellipse")
                {
                    Ellipses.Add(new GenericPostureEllipse(r));
                }
                else
                {
                    string outerXml = r.ReadOuterXml();
                    log.DebugFormat("Unparsed content in XML: {0}", outerXml);
                }
            }
            
            r.ReadEndElement();
        }
        private void ParseAngles(XmlReader r)
        {
            r.ReadStartElement();
            
            while(r.NodeType == XmlNodeType.Element)
            {
                if(r.Name == "Angle")
                {
                    Angles.Add(new GenericPostureAngle(r));
                }
                else
                {
                    string outerXml = r.ReadOuterXml();
                    log.DebugFormat("Unparsed content in XML: {0}", outerXml);
                }
            }
            
            r.ReadEndElement();
        }
        private void ParseHandles(XmlReader r)
        {
            r.ReadStartElement();
            
            while(r.NodeType == XmlNodeType.Element)
            {
                if(r.Name == "Handle")
                {
                    Handles.Add(new GenericPostureHandle(r));
                }
                else
                {
                    string outerXml = r.ReadOuterXml();
                    log.DebugFormat("Unparsed content in XML: {0}", outerXml);
                }
            }
            
            r.ReadEndElement();
        }
        private void ParseHitZone(XmlReader r)
        {
            r.ReadStartElement();
            
            while(r.NodeType == XmlNodeType.Element)
            {
                if(r.Name == "Polygon")
                {
                    HitZones.Add(new GenericPostureHitZonePolygon(r));
                }
                else
                {
                    string outerXml = r.ReadOuterXml();
                    log.DebugFormat("Unparsed content in XML: {0}", outerXml);
                }
            }
            
            r.ReadEndElement();
        }
        private void ParseInitialConfiguration(XmlReader r)
        {
            r.ReadStartElement();
            int index = 0;
            while(r.NodeType == XmlNodeType.Element)
            {
                if(r.Name == "Point")
                {
                    if(index < Points.Count)
                    {
                        Points[index] = XmlHelper.ParsePoint(r.ReadElementContentAsString());
                        index++;
                    }
                    else
                    {
                        string outerXml = r.ReadOuterXml();
                        log.DebugFormat("Unparsed point in initial configuration: {0}", outerXml);
                    }
                }
                else
                {
                    string outerXml = r.ReadOuterXml();
                    log.DebugFormat("Unparsed content: {0}", outerXml);
                }
            }
            
            r.ReadEndElement();
        }
        #endregion
    }
}