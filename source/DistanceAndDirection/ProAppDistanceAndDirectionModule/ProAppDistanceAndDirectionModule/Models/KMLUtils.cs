﻿/******************************************************************************* 
  * Copyright 2016 Esri 
  *  
  *  Licensed under the Apache License, Version 2.0 (the "License"); 
  *  you may not use this file except in compliance with the License. 
  *  You may obtain a copy of the License at 
  *  
  *  http://www.apache.org/licenses/LICENSE-2.0 
  *   
  *   Unless required by applicable law or agreed to in writing, software 
  *   distributed under the License is distributed on an "AS IS" BASIS, 
  *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
  *   See the License for the specific language governing permissions and 
  *   limitations under the License. 
  ******************************************************************************/

using ArcGIS.Core.Geometry;
// Esri
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
// System
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ProAppDistanceAndDirectionModule.Models
{
    class KMLUtils
    {
        /// <summary>
        /// Converts a layer to kml
        /// </summary>
        /// <param name="kmzOutputPath">Location of output file</param>
        /// <param name="datasetName">Name of output kmz file</param>
        /// <param name="mapview">MapView object</param>
        /// <returns></returns>
        public static async Task ConvertLayerToKML(string kmzOutputPath, string datasetName, MapView mapview)
        {
            try
            {   
                string nameNoExtension = Path.GetFileNameWithoutExtension(datasetName);

                List<object> projArg = new List<object>();
                var srout = SpatialReferenceBuilder.CreateSpatialReference(4326);
                string outshp = nameNoExtension + "_proj";
                string projshpPath = Path.Combine(kmzOutputPath, outshp + ".shp");
                string shppath = Path.Combine(kmzOutputPath, nameNoExtension + ".shp");
                projArg.Add(shppath);
                projArg.Add(projshpPath);
                projArg.Add(srout);
                var projvalueArray = Geoprocessing.MakeValueArray(projArg.ToArray());
                IGPResult projresult = await Geoprocessing.ExecuteToolAsync("Project_management", projvalueArray);
                
                List<object> arguments2 = new List<object>();
                arguments2.Add(outshp);
                string fullPath = Path.Combine(kmzOutputPath, datasetName);
                arguments2.Add(fullPath);

                var valueArray = Geoprocessing.MakeValueArray(arguments2.ToArray());
                IGPResult result = await Geoprocessing.ExecuteToolAsync("LayerToKML_conversion", valueArray);

                // Remove the layer from the TOC
                var layer = MapView.Active.GetSelectedLayers()[0];
                MapView.Active.Map.RemoveLayer(layer);

            }
            catch(Exception ex)
            {

            }
        }
        
    }

    
}
