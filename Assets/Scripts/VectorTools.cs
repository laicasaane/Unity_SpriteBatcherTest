using UnityEngine;

namespace vadersb.utils
{
    public static class VectorTools
    {
        /// <summary>
        ///   <para>Rotates point by supplied angle in radians, around the origin point [0;0]</para>
        /// </summary>
        /// <param name="angle">angle in radians. positive angle == CCW rotation in Unity! and CW rotation in Pixel Coords!</param>
        public static Vector2 Rotate(Vector2 src, float angle)
        {
            float curSin = Mathf.Sin(angle);
            float curCos = Mathf.Cos(angle);

            return new Vector2((src.x * curCos) - (src.y * curSin), (src.x * curSin) + (src.y * curCos));
        }
    }
}
