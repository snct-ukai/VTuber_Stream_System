using System;
using System.Linq;
using OpenCvSharp;
using Util;

namespace detection
{
    public static class CoordinateParser
    {
        private const float DefaultBlowRatio = 1.5f;
        private static Param _paramFiltered;

        public static ref Param Parse(in Complex[] landmarkPoints, in (int row, int col) shape, in Mat mat)
        {
            var eyeRatio = GetEyeRatio(landmarkPoints);
            var pupil = GetPupil(landmarkPoints, mat, eyeRatio);
            mat.Dispose();
            var blow = GetBlow(landmarkPoints);
            var mouth = GetMouth(landmarkPoints);
            HeadPoseEstimation.Solve(landmarkPoints, shape, out var rot);
            _paramFiltered = new Param
            {
                ParamAngleX = rot.yaw,
                ParamAngleY = rot.pitch,
                ParamAngleZ = rot.roll,
                ParamEyeLOpen = eyeRatio.left,
                ParamEyeROpen = eyeRatio.right,
                ParamEyeBallX = pupil.x,
                ParamEyeBallY = pupil.y,
                ParamBrowLY = blow.left,
                ParamBrowRY = blow.right,
                ParamMouthForm = mouth.x,
                ParamMouthOpenY = mouth.y,
                ParamCheek = 0,
                ParamBreath = Live2Dmodel.ModelManager.ParamBreath
            };

            return ref _paramFiltered;
        }

        private static Rect MakeRect(Complex[] points)
        {
            var left = (int)points.Min(t => t.Re);
            var top = (int)points.Min(t => t.Im);
            var width = (int)points.Max(t => t.Re) - left;
            var height = (int)points.Max(t => t.Im) - top;

            return new Rect(left, top, width, height);
        }

        private static (float left, float right) GetEyeRatio(in Complex[] points)
        {
            var leftRatio = Math.Abs((points[37].Im - points[41].Im) / (points[38].Re - points[37].Re));
            
            var rightRatio = Math.Abs((points[44].Im - points[46].Im) / (points[43].Re - points[44].Re));

            return (Math.Clamp((int)(leftRatio * 2) * 2 / 2f, 0, 1), Math.Clamp((int)(rightRatio * 2) * 2 / 2f, 0, 1));
        }

        private static (float x, float y) GetPupil(in Complex[] points, in Mat image, in (float left, float right) eyeRatio)
        {
            var pupils = new (float x, float y)[]
            {
                (-1f, -1f),
                (-1f, -1f)
            };

            var calPupil = new Func<Complex[], Mat, int, int, (float x, float y)>((points, image, a, b) =>
            {
                var rect = MakeRect(points[a .. b]);
                var eye = image.SubMat(rect);
                Cv2.CvtColor(eye, eye, ColorConversionCodes.BGR2GRAY);
                Cv2.BitwiseNot(eye.Threshold(0, 255, ThresholdTypes.Otsu), eye);
                var moments = eye.Moments(true);
                var x = (int)(moments.M10 / moments.M00);
                var y = (int)(moments.M01 / moments.M00);
                
                return (2.0f * x / rect.X - 1, 2.0f * y / rect.Y - 1);
            });
            
            if (eyeRatio.left > 0.3f)
            {
                pupils[0] = calPupil(points, image, 36, 41);
            }

            // ReSharper disable once InvertIf
            if (eyeRatio.right > 0.3f)
            {
                pupils[1] = calPupil(points, image, 42, 47);
            }

            return (Math.Max(pupils[0].x, pupils[1].x), Math.Max(pupils[0].y, pupils[1].y));
        }

        private static (float left, float right) GetBlow(in Complex[] points)
        {
            return
                (
                    Math.Clamp(
                        DefaultBlowRatio - (points[17] - points[30]).Magnitude / (points[36] - points[30]).Magnitude,
                        -1, 1), 
                    Math.Clamp(
                        DefaultBlowRatio - (points[26] - points[30]).Magnitude / (points[45] - points[30]).Magnitude,
                        -1, 1))
                ;
        }

        private static (float x, float y) GetMouth(in Complex[] points)
        {
            var mouthForm = 1 - (points[64].Real - points[61].Real)/ (points[35].Real - points[31].Real) * 2;
            var mouthOpenY = (int)((points[62].Im - points[65].Im) / (points[29].Im - points[30].Im) * 4) / 4f;
            mouthOpenY = Math.Clamp(Math.Abs(mouthOpenY), 0, 1);
            return (mouthForm, Math.Clamp(mouthOpenY, -1, 1));
        }
    }
}
