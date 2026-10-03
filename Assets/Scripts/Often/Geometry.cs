using UnityEngine;

namespace Assets.Scripts.Often
{
    public class Geometry
    {


        public static Vector3 ClampPositionInCube(Vector3 position, float radius, Transform cube)
        {
            if (SphereInCube(position, radius, cube))
            {
                return position;
            }
            else
            {
                Vector3 localPosition = GetLocalPosition(position, cube);
                float x = Mathf.Clamp(localPosition.x, -0.5f * cube.lossyScale.x + radius, 0.5f * cube.lossyScale.x - radius);
                float y = Mathf.Clamp(localPosition.y, -0.5f * cube.lossyScale.y + radius, 0.5f * cube.lossyScale.y - radius);
                float z = Mathf.Clamp(localPosition.z, -0.5f * cube.lossyScale.z + radius, 0.5f * cube.lossyScale.z - radius);
                Vector3 clampedLocalPosition = new Vector3(x, y, z);
                return GetWordPosition(clampedLocalPosition, cube);
            }
        }

        public static bool SphereInCube(Vector3 position, float radius, Transform cube)
        {
            Vector3 localPos = GetLocalPosition(position, cube);
            Vector3 cubeScale = cube.lossyScale;
            return Mathf.Abs(localPos.x) + radius - 0.5f * cubeScale.x <= 0.0f &&
            Mathf.Abs(localPos.y) + radius - 0.5f * cubeScale.y <= 0.0f &&
            Mathf.Abs(localPos.z) + radius - 0.5f * cubeScale.z <= 0.0f;
        }

        public static Vector3 GetLocalPosition(Vector3 wordPosition, Transform shape)
        {
            Vector3 deltaPos = wordPosition - shape.position;
            return new Vector3(Vector3.Dot(deltaPos, shape.right), Vector3.Dot(deltaPos, shape.up), Vector3.Dot(deltaPos, shape.forward));
        }

        public static Vector3 GetWordPosition(Vector3 localPosition, Transform shape)
        {
            return shape.position + localPosition.x * shape.right + localPosition.y * shape.up + localPosition.z * shape.forward;
        }
        public static Vector3 getPerpendicularToVector(Vector3 vector, Vector3 origin)
        {
            return origin - Vector3.Project(origin, vector);
        }

    }
}
