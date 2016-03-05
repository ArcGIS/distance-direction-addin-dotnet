﻿// Copyright 2016 Esri 
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ESRI.ArcGIS.esriSystem;
using ArcMapAddinGeodesyAndRange.Helpers;
using ESRI.ArcGIS.ArcMapUI;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using System.Text.RegularExpressions;

namespace ArcMapAddinGeodesyAndRange.ViewModels
{
    /// <summary>
    /// Base class for all the common properties, commands and events for tab items
    /// </summary>
    public class TabBaseViewModel : BaseViewModel
    {
        public TabBaseViewModel()
        {
            //properties
            LineType = LineTypes.Geodesic;
            LineDistanceType = DistanceTypes.Meters;

            //commands
            ClearGraphicsCommand = new RelayCommand(OnClearGraphics);
            ActivateToolCommand = new RelayCommand(OnActivateTool);
            EnterKeyCommand = new RelayCommand(OnEnterKeyCommand);

            // Mediator
            Mediator.Register(Constants.NEW_MAP_POINT, OnNewMapPointEvent);
            Mediator.Register(Constants.MOUSE_MOVE_POINT, OnMouseMoveEvent);
            Mediator.Register(Constants.TAB_ITEM_SELECTED, OnTabItemSelected);
        }

        #region Properties

        // lists to store GUIDs of graphics, temp feedback and map graphics
        private static List<string> TempGraphicsList = new List<string>();
        private static List<string> MapGraphicsList = new List<string>();

        internal bool HasPoint1 = false;
        internal bool HasPoint2 = false;
        internal INewLineFeedback feedback = null;

        private IPoint point1 = null;
        /// <summary>
        /// Property for the first IPoint
        /// </summary>
        public virtual IPoint Point1
        {
            get
            {
                return point1;
            }
            set
            {
                // do not add anything to the map from here
                point1 = value;
                RaisePropertyChanged(() => Point1);
                RaisePropertyChanged(() => Point1Formatted);
            }
        }

        private IPoint point2 = null;
        /// <summary>
        /// Property for the second IPoint
        /// Not all tools need a second point
        /// </summary>
        public virtual IPoint Point2
        {
            get
            {
                return point2;
            }
            set
            {
                point2 = value;
                RaisePropertyChanged(() => Point2);
                RaisePropertyChanged(() => Point2Formatted);
            }
        }
        string point1Formatted = string.Empty;
        /// <summary>
        /// String property for the first IPoint
        /// This is used to format the point for the UI and allow string input of different types of coordinates
        /// </summary>
        public string Point1Formatted
        {
            get 
            {
                // return a formatted first point depending on how it was entered, manually or via map point tool
                if (string.IsNullOrWhiteSpace(point1Formatted))
                {
                    if (Point1 == null)
                        return string.Empty;

                    // only format if the Point1 data was generated from a mouse click
                    return string.Format("{0:0.0#####} {1:0.0#####}", Point1.Y, Point1.X);
                }
                else
                {
                    // this was user inputed so just return the inputed string
                    return point1Formatted;
                }
            }

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    point1Formatted = string.Empty;
                    RaisePropertyChanged(() => Point1Formatted);
                    return;
                }
                // try to convert string to an IPoint
                var point = GetPointFromString(value);
                if(point != null)
                {
                    // clear temp graphics
                    ClearTempGraphics();
                    point1Formatted = value;
                    HasPoint1 = true;
                    Point1 = point;
                    var color = new RgbColorClass() { Green = 255 } as IColor;
                    AddGraphicToMap(Point1, color, true);
                    // lets try feedback
                    var mxdoc = ArcMap.Application.Document as IMxDocument;
                    var av = mxdoc.FocusMap as IActiveView;
                    point.Project(mxdoc.FocusMap.SpatialReference);
                    CreateFeedback(point, av);
                    feedback.Start(point);
                    if(Point2 != null)
                    {
                        UpdateDistance(GetGeoPolylineFromPoints(Point1, Point2));
                        FeedbackMoveTo(Point2);
                    }
                }
                else 
                {
                    // invalid coordinate, reset and throw exception
                    Point1 = null;
                    HasPoint1 = false;
                    throw new ArgumentException(Properties.Resources.AEInvalidCoordinate);
                }
            }
        }

        string point2Formatted = string.Empty;
        /// <summary>
        /// String property for the second IPoint
        /// This is used to format the point for the UI and allow string input of different types of coordinates
        /// Input types like GARS, MGRS, USNG, UTM
        /// </summary>
        public string Point2Formatted
        {
            get
            {
                // return a formatted second point depending on how it was entered, manually or via map point tool
                if (string.IsNullOrWhiteSpace(point2Formatted))
                {
                    if (Point2 == null)
                        return string.Empty;

                    // only format if the Point2 data was generated from a mouse click
                    return string.Format("{0:0.0#####} {1:0.0#####}", Point2.Y, Point2.X);
                }
                else
                {
                    // this was user inputed so just return the inputed string
                    return point2Formatted;
                }
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    point2Formatted = string.Empty;
                    RaisePropertyChanged(() => Point2Formatted);
                    return;
                }
                // try to convert string to an IPoint
                var point = GetPointFromString(value);
                if (point != null)
                {
                    point2Formatted = value;
                    //HasPoint2 = true;
                    Point2 = point;
                    var mxdoc = ArcMap.Application.Document as IMxDocument;
                    var av = mxdoc.FocusMap as IActiveView;
                    Point2.Project(mxdoc.FocusMap.SpatialReference);

                    //if (feedback != null)
                    //{
                    //    // I have to create a new point here, otherwise "MoveTo" will change the spatial reference to world mercator
                    //    FeedbackMoveTo(point);
                    //}
                    if (HasPoint1)
                    {
                        // lets try feedback
                        CreateFeedback(Point1, av);
                        feedback.Start(Point1);
                        UpdateDistance(GetGeoPolylineFromPoints(Point1, Point2));
                        // I have to create a new point here, otherwise "MoveTo" will change the spatial reference to world mercator
                        FeedbackMoveTo(point);
                    }

                }
                else
                {
                    // invalid coordinate, reset and throw exception
                    Point2 = null;
                    HasPoint2 = false;
                    throw new ArgumentException(Properties.Resources.AEInvalidCoordinate);
                }
            }
        }


        private bool isActiveTab = false;
        /// <summary>
        /// Property to keep track of which tab/viewmodel is the active item
        /// </summary>
        public bool IsActiveTab
        {
            get
            {
                return isActiveTab;
            }
            set
            {
                Reset(true);
                isActiveTab = value;
                RaisePropertyChanged(() => IsActiveTab);
            }
        }

        DistanceTypes lineDistanceType = DistanceTypes.Meters;
        /// <summary>
        /// Property for the distance type
        /// </summary>
        public DistanceTypes LineDistanceType
        {
            get { return lineDistanceType; }
            set
            {
                var before = lineDistanceType;
                lineDistanceType = value;
                UpdateDistanceFromTo(before, value);
            }
        }

        double distance = 0.0;
        /// <summary>
        /// Property for the distance/length
        /// </summary>
        public virtual double Distance
        {
            get { return distance; }
            set
            {
                if ( value < 0.0 )
                    throw new ArgumentException(Properties.Resources.AEMustBePositive);

                distance = value;
                DistanceString = distance.ToString("N"); // use current culture number format
                RaisePropertyChanged(() => Distance);
                RaisePropertyChanged(() => DistanceString);
            }
        }
        string distanceString = String.Empty;
        /// <summary>
        /// Distance property as a string
        /// </summary>
        public virtual string DistanceString
        {
            get
            {
                return Distance.ToString("N"); // use current culture number format
            }
            set
            {
                // lets avoid an infinite loop here
                if (string.Equals(distanceString, value))
                    return;

                distanceString = value;

                // update distance
                double d = 0.0;
                if (double.TryParse(distanceString, out d))
                {
                    Distance = d;
                }
                else
                {
                    throw new ArgumentException(Properties.Resources.AEInvalidInput);
                }
            }
        }

        /// <summary>
        /// Property for the type of geodesy line
        /// </summary>
        public LineTypes LineType { get; set; }

        /// <summary>
        /// Property used to test if there is enough info to create a line map element
        /// </summary>
        public virtual bool CanCreateElement
        {
            get
            {
                return (Point1 != null && Point2 != null);
            }
        }


        #endregion Properties

        #region Commands

        public RelayCommand ClearGraphicsCommand { get; set; }
        public RelayCommand ActivateToolCommand { get; set; }
        public RelayCommand EnterKeyCommand { get; set; }
        
        #endregion

        /// <summary>
        /// Method is called when a user pressed the "Enter" key or when a second point is created for a line from mouse clicks
        /// Derived class must override this method in order to create map elements
        /// Clears temp graphics by default
        /// </summary>
        internal virtual void CreateMapElement()
        {
            ClearTempGraphics();
        }

        #region Private Event Functions

        /// <summary>
        /// Clears all the graphics from the maps graphic container
        /// Inlucdes temp and map graphics
        /// Only removes temp and map graphics that were created by this add-in
        /// </summary>
        /// <param name="obj"></param>
        private void OnClearGraphics(object obj)
        {
            var mxdoc = ArcMap.Application.Document as IMxDocument;
            if (mxdoc == null)
                return;
            var av = mxdoc.FocusMap as IActiveView;
            if (av == null)
                return;
            var gc = av as IGraphicsContainer;
            if (gc == null)
                return;

            RemoveGraphics(gc, TempGraphicsList);
            RemoveGraphics(gc, MapGraphicsList);
            
            //gc.DeleteAllElements();
            //av.Refresh();
			av.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }

        /// <summary>
        /// Method to clear all temp graphics
        /// </summary>
        internal void ClearTempGraphics()
        {
            var mxdoc = ArcMap.Application.Document as IMxDocument;
            if (mxdoc == null)
                return;
            var av = mxdoc.FocusMap as IActiveView;
            if (av == null)
                return;
            var gc = av as IGraphicsContainer;
            if (gc == null)
                return;

            RemoveGraphics(gc, TempGraphicsList);

            av.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }
        /// <summary>
        /// Method used to remove graphics from the graphics container
        /// Elements are tagged with a GUID on the IElementProperties.Name property
        /// </summary>
        /// <param name="gc">map graphics container</param>
        /// <param name="list">list of GUIDs to remove</param>
        private void RemoveGraphics(IGraphicsContainer gc, List<string> list)
        {
            if (gc == null || !list.Any())
                return;

            var elementList = new List<IElement>();
            gc.Reset();
            var element = gc.Next();
            while (element != null)
            {
                var eleProps = element as IElementProperties;
                if (list.Contains(eleProps.Name))
                {
                    elementList.Add(element);
                }
                element = gc.Next();
            }

            foreach (var ele in elementList)
            {
                gc.DeleteElement(ele);
            }

            list.Clear();
            elementList.Clear();
        }

        /// <summary>
        /// Activates the map tool to get map points from mouse clicks/movement
        /// </summary>
        /// <param name="obj"></param>
        private void OnActivateTool(object obj)
        {
            SetToolActiveInToolBar(ArcMap.Application, "Esri_ArcMapAddinGeodesyAndRange_MapPointTool");
        }
        /// <summary>
        /// Handler for the "Enter"key command
        /// Calls CreateMapElement
        /// </summary>
        /// <param name="obj"></param>
        internal virtual void OnEnterKeyCommand(object obj)
        {
            var depends = obj as System.Windows.DependencyObject;

            // check all children of dependency object for validation errors
            if (depends != null && !IsValid(depends))
                return;

            if (!CanCreateElement)
                return;

            CreateMapElement();
        }

        private bool IsValid(System.Windows.DependencyObject obj)
        {
            // The dependency object is valid if it has no errors and all
            // of its children (that are dependency objects) are error-free.
            return !Validation.GetHasError(obj) &&
            System.Windows.LogicalTreeHelper.GetChildren(obj)
            .OfType<System.Windows.DependencyObject>()
            .All(IsValid);
        }

        /// <summary>
        /// Handler for the new map point click event
        /// </summary>
        /// <param name="obj">IPoint</param>
        internal virtual void OnNewMapPointEvent(object obj)
        {
            if (!IsActiveTab)
                return;

            var mxdoc = ArcMap.Application.Document as IMxDocument;
            var av = mxdoc.FocusMap as IActiveView;
            var point = obj as IPoint;

            if (point == null)
                return;

            if (!HasPoint1)
            {
                // clear temp graphics
                ClearTempGraphics();
                Point1 = point;
                HasPoint1 = true;
                Point1Formatted = string.Empty;

                var color = new RgbColorClass() { Green = 255 } as IColor;
                AddGraphicToMap(Point1, color, true);

                // lets try feedback
                CreateFeedback(point, av);
                feedback.Start(point);
            }
            else if (!HasPoint2)
            {
                ResetFeedback();
                Point2 = point;
                HasPoint2 = true;
                point2Formatted = string.Empty;
                RaisePropertyChanged(() => Point2Formatted);
            }

            if (HasPoint1 && HasPoint2)
            {
                CreateMapElement();
                ResetPoints();
            }
        }

        #endregion

        #region Public Functions
        /// <summary>
        /// Method used to deactivate tool
        /// </summary>
        public void DeactivateTool(string toolname)
        {
            if (ArcMap.Application != null
                && ArcMap.Application.CurrentTool != null
                && ArcMap.Application.CurrentTool.Name.Equals(toolname))
            {
                ArcMap.Application.CurrentTool = null;
            }
        }
        /// <summary>
        /// Method to set the map tool as the active tool for the map
        /// </summary>
        /// <param name="application"></param>
        /// <param name="toolName"></param>
        public void SetToolActiveInToolBar(ESRI.ArcGIS.Framework.IApplication application, System.String toolName)
        {
            ESRI.ArcGIS.Framework.ICommandBars commandBars = application.Document.CommandBars;
            ESRI.ArcGIS.esriSystem.UID commandID = new ESRI.ArcGIS.esriSystem.UIDClass();
            commandID.Value = toolName;
            ESRI.ArcGIS.Framework.ICommandItem commandItem = commandBars.Find(commandID, false, false);

            if (commandItem != null)
                application.CurrentTool = commandItem;
        }
        #endregion

        #region Private Functions

        /// <summary>
        /// Method used to totally reset the tool
        /// reset points, feedback
        /// clear out textboxes
        /// </summary>
        internal virtual void Reset(bool toolReset)
        {
            if (toolReset)
            {
                DeactivateTool("Esri_ArcMapAddinGeodesyAndRange_MapPointTool");
            }

            ResetPoints();
            Point1 = null;
            Point2 = null;
            Point1Formatted = string.Empty;
            Point2Formatted = string.Empty;

            ResetFeedback();

            Distance = 0.0;

            ClearTempGraphics();
        }
        /// <summary>
        /// Resets Points 1 and 2
        /// </summary>
        internal virtual void ResetPoints()
        {
            HasPoint1 = HasPoint2 = false;
        }

        /// <summary>
        /// Resets feedback aka cancels feedback
        /// </summary>
        internal void ResetFeedback()
        {
            if (feedback == null)
                return;

            feedback.Stop();
            feedback = null;
        }

        /// <summary>
        /// Handler for the tab item selected event
        /// Helps keep track of which tab item/viewmodel is active
        /// </summary>
        /// <param name="obj">bool if selected or not</param>
        private void OnTabItemSelected(object obj)
        {
            if (obj == null)
                return;

            IsActiveTab = (obj == this);
        }

        /// <summary>
        /// Adds a graphic element to the map graphics container
        /// </summary>
        /// <param name="geom">IGeometry</param>
        internal void AddGraphicToMap(IGeometry geom, IColor color, bool IsTempGraphic = false, esriSimpleMarkerStyle markerStyle = esriSimpleMarkerStyle.esriSMSCircle, esriRasterOpCode rasterOpCode = esriRasterOpCode.esriROPNOP)
        {
            if (geom == null || ArcMap.Document == null || ArcMap.Document.FocusMap == null)
                return;
            IElement element = null;
            //ESRI.ArcGIS.Display.IRgbColor rgbColor = new ESRI.ArcGIS.Display.RgbColorClass();
            //rgbColor.Red = 255;
            //ESRI.ArcGIS.Display.IColor color = rgbColor; // Implicit cast.
            double width = 2.0;

            geom.Project(ArcMap.Document.FocusMap.SpatialReference);

            if(geom.GeometryType == esriGeometryType.esriGeometryPoint)
            {
                // Marker symbols
                var simpleMarkerSymbol = new SimpleMarkerSymbol() as ISimpleMarkerSymbol;
                simpleMarkerSymbol.Color = color;
                simpleMarkerSymbol.Outline = true;
                simpleMarkerSymbol.OutlineColor = color;
                simpleMarkerSymbol.Size = 5;
                simpleMarkerSymbol.Style = markerStyle;

                var markerElement = new MarkerElement() as IMarkerElement;
                markerElement.Symbol = simpleMarkerSymbol;
                element = markerElement as IElement;
            }
            else if(geom.GeometryType == esriGeometryType.esriGeometryPolyline)
            {
                // create graphic then add to map
                var lineSymbol = new SimpleLineSymbolClass();
                lineSymbol.Color = color;
                lineSymbol.Width = width;
                if (IsTempGraphic && rasterOpCode != esriRasterOpCode.esriROPNOP)
                {
                    lineSymbol.Width = 1;
                    lineSymbol.ROP2 = rasterOpCode;
                }

                var le = new LineElementClass() as ILineElement;
                element = le as IElement;
                le.Symbol = lineSymbol;
            }

            if (element == null)
                return;

            element.Geometry = geom;

            var mxdoc = ArcMap.Application.Document as IMxDocument;
            var av = mxdoc.FocusMap as IActiveView;
            var gc = av as IGraphicsContainer;

            // store guid
            var eprop = element as IElementProperties;
            eprop.Name = Guid.NewGuid().ToString();
            
            if (IsTempGraphic)
                TempGraphicsList.Add(eprop.Name);
            else
                MapGraphicsList.Add(eprop.Name);

            gc.AddElement(element, 0);

            //refresh map
            av.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }
        internal void AddGraphicToMap(IGeometry geom, bool IsTempGraphic = false)
        {
            var color = new RgbColorClass() { Red = 255 } as IColor;
            AddGraphicToMap(geom, color, IsTempGraphic);
        }
        internal ISpatialReferenceFactory3 srf3 = null;
        /// <summary>
        /// Gets the linear unit from the esri constants for linear units
        /// </summary>
        /// <returns>ILinearUnit</returns>
        internal ILinearUnit GetLinearUnit()
        {
            int unitType = (int)esriSRUnitType.esriSRUnit_Meter;
             if (srf3 == null)
            {
                Type srType = Type.GetTypeFromProgID("esriGeometry.SpatialReferenceEnvironment");
                srf3 = Activator.CreateInstance(srType) as ISpatialReferenceFactory3;
            }

            switch (LineDistanceType)
            {
                case DistanceTypes.Feet:
                    unitType = (int)esriSRUnitType.esriSRUnit_Foot;
                    break;
                case DistanceTypes.Kilometers:
                    unitType = (int)esriSRUnitType.esriSRUnit_Kilometer;
                    break;
                case DistanceTypes.Meters:
                    unitType = (int)esriSRUnitType.esriSRUnit_Meter;
                    break;
                case DistanceTypes.NauticalMile:
                    unitType = (int)esriSRUnitType.esriSRUnit_NauticalMile;
                    break;
                case DistanceTypes.SurveyFoot:
                    unitType = (int)esriSRUnitType.esriSRUnit_SurveyFoot;
                    break;
                default:
                    unitType = (int)esriSRUnitType.esriSRUnit_Meter;
                    break;
            }

            return srf3.CreateUnit(unitType) as ILinearUnit;
        }

        /// <summary>
        /// Ugly method to convert to/from different types of distance units
        /// </summary>
        /// <param name="fromType">DistanceTypes</param>
        /// <param name="toType">DistanceTypes</param>
        internal void UpdateDistanceFromTo(DistanceTypes fromType, DistanceTypes toType)
        {
            try
            {
                double length = Distance;

                if (fromType == DistanceTypes.Meters && toType == DistanceTypes.Kilometers)
                    length /= 1000.0;
                else if (fromType == DistanceTypes.Meters && toType == DistanceTypes.Feet)
                    length *= 3.28084;
                else if (fromType == DistanceTypes.Meters && toType == DistanceTypes.SurveyFoot)
                    length *= 3.280833333;
                else if (fromType == DistanceTypes.Meters && toType == DistanceTypes.NauticalMile)
                    length *= 0.000539957;
                else if (fromType == DistanceTypes.Kilometers && toType == DistanceTypes.Meters)
                    length *= 1000.0;
                else if (fromType == DistanceTypes.Kilometers && toType == DistanceTypes.Feet)
                    length *= 3280.84;
                else if (fromType == DistanceTypes.Kilometers && toType == DistanceTypes.SurveyFoot)
                    length *= 3280.833333;
                else if (fromType == DistanceTypes.Kilometers && toType == DistanceTypes.NauticalMile)
                    length *= 0.539957;
                else if (fromType == DistanceTypes.Feet && toType == DistanceTypes.Kilometers)
                    length *= 0.0003048;
                else if (fromType == DistanceTypes.Feet && toType == DistanceTypes.Meters)
                    length *= 0.3048;
                else if (fromType == DistanceTypes.Feet && toType == DistanceTypes.SurveyFoot)
                    length *= 0.999998000004;
                else if (fromType == DistanceTypes.Feet && toType == DistanceTypes.NauticalMile)
                    length *= 0.000164579;
                else if (fromType == DistanceTypes.SurveyFoot && toType == DistanceTypes.Kilometers)
                    length *= 0.0003048006096;
                else if (fromType == DistanceTypes.SurveyFoot && toType == DistanceTypes.Meters)
                    length *= 0.3048006096;
                else if (fromType == DistanceTypes.SurveyFoot && toType == DistanceTypes.Feet)
                    length *= 1.000002;
                else if (fromType == DistanceTypes.SurveyFoot && toType == DistanceTypes.NauticalMile)
                    length *= 0.00016457916285097;
                else if (fromType == DistanceTypes.NauticalMile && toType == DistanceTypes.Kilometers)
                    length *= 1.852001376036;
                else if (fromType == DistanceTypes.NauticalMile && toType == DistanceTypes.Meters)
                    length *= 1852.001376036;
                else if (fromType == DistanceTypes.NauticalMile && toType == DistanceTypes.Feet)
                    length *= 6076.1154855643;
                else if (fromType == DistanceTypes.NauticalMile && toType == DistanceTypes.SurveyFoot)
                    length *= 6076.1033333576;

                Distance = length;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Get the currently selected geodetic type
        /// </summary>
        /// <returns>esriGeodeticType</returns>
        internal esriGeodeticType GetEsriGeodeticType()
        {
            esriGeodeticType type = esriGeodeticType.esriGeodeticTypeGeodesic;

            switch (LineType)
            {
                case LineTypes.Geodesic:
                    type = esriGeodeticType.esriGeodeticTypeGeodesic;
                    break;
                case LineTypes.GreatElliptic:
                    type = esriGeodeticType.esriGeodeticTypeGreatElliptic;
                    break;
                case LineTypes.Loxodrome:
                    type = esriGeodeticType.esriGeodeticTypeLoxodrome;
                    break;
                default:
                    type = esriGeodeticType.esriGeodeticTypeGeodesic;
                    break;
            }

            return type;
        }
        internal double GetGeodeticLengthFromPolyline(IPolyline polyline)
        {
            if (polyline == null)
                return 0.0;

            var polycurvegeo = polyline as IPolycurveGeodetic;

            var geodeticType = GetEsriGeodeticType();
            var linearUnit = GetLinearUnit();
            var geodeticLength = polycurvegeo.get_LengthGeodetic(geodeticType, linearUnit);

            return geodeticLength;
        }
        /// <summary>
        /// Gets the distance/lenght of a polyline
        /// </summary>
        /// <param name="geometry">IGeometry</param>
        internal void UpdateDistance(IGeometry geometry)
        {
            var polyline = geometry as IPolyline;

            if (polyline == null)
                return;

            Distance = GetGeodeticLengthFromPolyline(polyline);
        }
        /// <summary>
        /// Handler for the mouse move event
        /// When the mouse moves accross the map, IPoints are returned to aid in updating feedback to user
        /// </summary>
        /// <param name="obj">IPoint</param>
        internal virtual void OnMouseMoveEvent(object obj)
        {
            if (!IsActiveTab)
                return;

            var point = obj as IPoint;

            if (point == null)
                return;

            // dynamically update start point if not set yet
            if (!HasPoint1)
            {
                Point1 = point;
            }
            else if (HasPoint1 && !HasPoint2)
            {
                Point2Formatted = string.Empty;
                Point2 = point;
                // get distance from feedback
                var polyline = GetGeoPolylineFromPoints(Point1, point);
                UpdateDistance(polyline);
            }

            // update feedback
            if (HasPoint1 && !HasPoint2)
            {
                FeedbackMoveTo(point);
            }
        }
        /// <summary>
        /// Gets a geodetic polyline from two points
        /// startPoint is where it will restart from
        /// endPoint is where you want it to end for the return of the polyline
        /// </summary>
        /// <param name="startPoint">startPoint is where it will restart from</param>
        /// <param name="endPoint">endPoint is where you want it to end for the return of the polyline</param>
        /// <returns>IPolyline</returns>
        internal IPolyline GetGeoPolylineFromPoints(IPoint startPoint, IPoint endPoint)
        {
            var construct = new Polyline() as IConstructGeodetic;
            if (construct == null)
                return null;

            construct.ConstructGeodeticLineFromPoints(GetEsriGeodeticType(), startPoint, endPoint, GetLinearUnit(), esriCurveDensifyMethod.esriCurveDensifyByDeviation, -1.0);

            return construct as IPolyline;
        }

        /// <summary>
        /// Creates a new geodetic line feedback to visualize the line to the user
        /// </summary>
        /// <param name="point">IPoint, start point</param>
        /// <param name="av">The current active view</param>
        internal void CreateFeedback(IPoint point, IActiveView av)
        {
            ResetFeedback();
            feedback = new NewLineFeedback();
            var geoFeedback = feedback as IGeodeticLineFeedback;
            geoFeedback.GeodeticConstructionMethod = GetEsriGeodeticType();
            geoFeedback.UseGeodeticConstruction = true;
            geoFeedback.SpatialReference = point.SpatialReference;
            var displayFB = feedback as IDisplayFeedback;
            displayFB.Display = av.ScreenDisplay;
        }
        /// <summary>
        /// Method used to convert a string to a known coordinate
        /// Assumes WGS84 for now
        /// Uses the IConversionNotation interface
        /// </summary>
        /// <param name="coordinate">the coordinate as a string</param>
        /// <returns>IPoint if successful, null if not</returns>
        internal IPoint GetPointFromString(string coordinate)
        {
            Type t = Type.GetTypeFromProgID("esriGeometry.SpatialReferenceEnvironment");
            System.Object obj = Activator.CreateInstance(t);
            ISpatialReferenceFactory srFact = obj as ISpatialReferenceFactory;

            // Use the enumeration to create an instance of the predefined object.

            IGeographicCoordinateSystem geographicCS =
                srFact.CreateGeographicCoordinateSystem((int)
                esriSRGeoCSType.esriSRGeoCS_WGS1984);

            var point = new Point() as IPoint;
            point.SpatialReference = geographicCS;
            var cn = point as IConversionNotation;

            if (cn == null)
                return null;

            try { cn.PutCoordsFromDD(coordinate); return point; } catch { }
            try { cn.PutCoordsFromDDM(coordinate); return point; } catch { }
            try { cn.PutCoordsFromDMS(coordinate); return point; } catch { }
            try { cn.PutCoordsFromGARS(esriGARSModeEnum.esriGARSModeCENTER, coordinate); return point; } catch { }
            try { cn.PutCoordsFromGARS(esriGARSModeEnum.esriGARSModeLL, coordinate); return point; } catch { }
            try { cn.PutCoordsFromMGRS(coordinate, esriMGRSModeEnum.esriMGRSMode_Automatic); return point; } catch { }
            try { cn.PutCoordsFromMGRS(coordinate, esriMGRSModeEnum.esriMGRSMode_NewStyle); return point; } catch { } 
            try { cn.PutCoordsFromMGRS(coordinate, esriMGRSModeEnum.esriMGRSMode_NewWith180InZone01); return point; } catch { } 
            try { cn.PutCoordsFromMGRS(coordinate, esriMGRSModeEnum.esriMGRSMode_OldStyle); return point; } catch { }
            try { cn.PutCoordsFromMGRS(coordinate, esriMGRSModeEnum.esriMGRSMode_OldWith180InZone01); return point; } catch { }
            try { cn.PutCoordsFromMGRS(coordinate, esriMGRSModeEnum.esriMGRSMode_USNG); return point; } catch { }
            try { cn.PutCoordsFromUSNG(coordinate); return point; } catch { }
            try { cn.PutCoordsFromUTM(esriUTMConversionOptionsEnum.esriUTMAddSpaces, coordinate); return point; } catch { }
            try { cn.PutCoordsFromUTM(esriUTMConversionOptionsEnum.esriUTMUseNS, coordinate); return point; } catch { }
            try { cn.PutCoordsFromUTM(esriUTMConversionOptionsEnum.esriUTMAddSpaces|esriUTMConversionOptionsEnum.esriUTMUseNS, coordinate); return point; } catch { }
            try { cn.PutCoordsFromUTM(esriUTMConversionOptionsEnum.esriUTMNoOptions, coordinate); return point; } catch { }
            try { cn.PutCoordsFromGeoRef(coordinate); return point; } catch { }

            // lets see if we have a PCS coordinate
            // we'll assume the same units as the map units
            // get spatial reference of map
            if (ArcMap.Document == null || ArcMap.Document.FocusMap == null || ArcMap.Document.FocusMap.SpatialReference == null)
                return null;

            var map = ArcMap.Document.FocusMap;
            var pcs = map.SpatialReference as IProjectedCoordinateSystem;

            if (pcs == null)
                return null;

            point.SpatialReference = map.SpatialReference;
            // get pcs coordinate from input
            coordinate = coordinate.Trim();

            Regex regexMercator = new Regex(@"^(?<latitude>\-?\d+\.?\d*)[+,;:\s]*(?<longitude>\-?\d+\.?\d*)");

            var matchMercator = regexMercator.Match(coordinate);

            if (matchMercator.Success && matchMercator.Length == coordinate.Length)
            {
                try
                {
                    var Lat = Double.Parse(matchMercator.Groups["latitude"].Value);
                    var Lon = Double.Parse(matchMercator.Groups["longitude"].Value);
                    point.PutCoords(Lon, Lat);
                    return point;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
        /// <summary>
        /// Method to use when you need to move a feedback line to a point
        /// This forces a new point to be used, sometimes this method projects the point to a different spatial reference
        /// </summary>
        /// <param name="point"></param>
        internal void FeedbackMoveTo(IPoint point)
        {
            if (feedback == null || point == null)
                return;

            feedback.MoveTo(new Point() { X = point.X, Y = point.Y, SpatialReference = point.SpatialReference });
        }
        #endregion Private Functions

    }
}
