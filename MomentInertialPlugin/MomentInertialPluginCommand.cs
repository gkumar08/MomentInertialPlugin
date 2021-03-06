﻿using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace MomentInertialPlugin
{
    [System.Runtime.InteropServices.Guid("2413f559-bf30-4e21-9563-84139beb3357")]
    public class MomentInertialPluginCommand : Command
    {
        public MomentInertialPluginCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static MomentInertialPluginCommand Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "MomentInertialPluginCommand"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            //select the cross-section faces
            var gc = new Rhino.Input.Custom.GetObject();
            gc.SetCommandPrompt("Select the surfaces which form the cross-section of the beam.");
            gc.GeometryFilter = Rhino.DocObjects.ObjectType.Surface;
            gc.EnablePreSelect(false, true);
            gc.GetMultiple(1, 1);
            if (gc.CommandResult() != Rhino.Commands.Result.Success)
                return gc.CommandResult();

            var face = gc.Object(0).Face();

            //for each cross-section, get the base-surface curve
            var gv = new Rhino.Input.Custom.GetObject();
            gv.SetCommandPrompt("Select the base_curve.");
            gv.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            gv.EnablePreSelect(false, true);
            gv.GetMultiple(1, 1);
            if (gv.CommandResult() != Rhino.Commands.Result.Success)
                return gv.CommandResult();

            var base_curve = gv.Object(0).Curve();

            //List of all the sub section
            List<Brep> sub_sections = new List<Brep>();
            for (int i = 0; i < 10; i++)
            {

                //offset the base to form a closed loop
                var curves = base_curve.OffsetOnSurface(face, 0.1, doc.ModelAbsoluteTolerance);
                if (curves.Length > 1)
                {
                    Rhino.RhinoApp.WriteLine("More than one offset");
                    return Result.Failure;
                }
                var offset_curve = curves[0];

                //connect the curves in a closed loop
                LineCurve edge1 = new LineCurve(base_curve.PointAtStart, offset_curve.PointAtStart);
                LineCurve edge2 = new LineCurve(offset_curve.PointAtEnd, base_curve.PointAtEnd);
                List<Curve> prejoin = new List<Curve>();
                prejoin.Add(edge1);
                prejoin.Add(edge2);
                prejoin.Add(base_curve);
                prejoin.Add(offset_curve);

                var loops = Curve.JoinCurves(prejoin, doc.ModelAbsoluteTolerance);

                if (loops.Length > 1)
                {
                    Rhino.RhinoApp.WriteLine("More than one joined loops");
                    return Result.Failure;
                }
                var loop = loops[0];
                var breps = Rhino.Geometry.Brep.CreatePlanarBreps(loop);

                if (breps.Length > 1)
                {
                    Rhino.RhinoApp.WriteLine("More than one joined loops");
                    return Result.Failure;
                }

                sub_sections.Add(breps[0]);

                doc.Objects.AddBrep(breps[0]);
                doc.Views.Redraw();

                base_curve = offset_curve;
            }

            //compute moment of inertia
            var area_properties1 = Rhino.Geometry.AreaMassProperties.Compute(sub_sections);
            var area_properties2 = Rhino.Geometry.AreaMassProperties.Compute(face);

            //check for the difference
            Rhino.RhinoApp.WriteLine("Area {0}", area_properties1.Area);
            Rhino.RhinoApp.WriteLine("Difference in Area {0}", area_properties1.Area - area_properties2.Area);

            Rhino.RhinoApp.WriteLine("Moment X: {0}; Y: {1}; Z: {2} in WCS ", area_properties1.WorldCoordinatesMomentsOfInertia.X, area_properties1.WorldCoordinatesMomentsOfInertia.Y, area_properties1.WorldCoordinatesMomentsOfInertia.Z);
            Rhino.RhinoApp.WriteLine("Difference in Moment X: {0}; Y: {1}; Z: {2} in WCS", area_properties1.WorldCoordinatesMomentsOfInertia.X - area_properties2.WorldCoordinatesMomentsOfInertia.X,                 area_properties1.WorldCoordinatesMomentsOfInertia.Y - area_properties2.WorldCoordinatesMomentsOfInertia.Y, area_properties1.WorldCoordinatesMomentsOfInertia.Z - area_properties2.WorldCoordinatesMomentsOfInertia.Z);

            //display the centroids
            doc.Objects.AddPoint(area_properties1.Centroid);
            doc.Objects.AddPoint(area_properties2.Centroid);

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
