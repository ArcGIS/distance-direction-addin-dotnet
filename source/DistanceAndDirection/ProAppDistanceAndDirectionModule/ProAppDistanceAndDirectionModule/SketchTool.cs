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
using System.Threading.Tasks;
using System.Reactive.Linq;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using DistanceAndDirectionLibrary.Helpers;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Reactive.Subjects;

namespace ProAppDistanceAndDirectionModule
{
    class SketchTool : MapTool
    {
        public SketchTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Point;
            UseSnapping = true;
            // will need to use this in the future, commented out for now
            //Mediator.Register("SET_SKETCH_TOOL_TYPE", (sgType) => SketchType = (SketchGeometryType)sgType);

            //lets limit how many times we call this
            // take the latest event args every so often
            // this will keep us from drawing too many feedback geometries
            mouseSubject.Sample(TimeSpan.FromMilliseconds(150)).Subscribe(async (x) =>
                {
                    var mp = await QueuedTask.Run(() =>
                    {
                        return MapView.Active.ClientToMap(x.ClientPoint);
                    });
                    Mediator.NotifyColleagues(DistanceAndDirectionLibrary.Constants.MOUSE_MOVE_POINT, mp);
                });

        }
        Subject<MapViewMouseEventArgs> mouseSubject = new Subject<MapViewMouseEventArgs>();

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            try
            {
                var mp = geometry as MapPoint;
                Mediator.NotifyColleagues(DistanceAndDirectionLibrary.Constants.NEW_MAP_POINT, mp);
            }
            catch(Exception ex)
            {
                // do nothing
            }

            return base.OnSketchCompleteAsync(geometry);
        }

        protected override void OnToolMouseMove(MapViewMouseEventArgs e)
        {
            try
            {
                // try a subject here to limit the amount of times this is handled
                mouseSubject.OnNext(e);
            }
            catch(Exception ex)
            {
                // do nothing
            }
            base.OnToolMouseMove(e);
        }

        protected override async void OnToolDoubleClick(MapViewMouseButtonEventArgs e)
        {
            try
            {
                var mp = await QueuedTask.Run(() =>
                {
                    return MapView.Active.ClientToMap(e.ClientPoint);
                });
                Mediator.NotifyColleagues(DistanceAndDirectionLibrary.Constants.MOUSE_DOUBLE_CLICK, mp);
            }
            catch(Exception ex)
            {

            }
            base.OnToolDoubleClick(e);
        }
    }
}
