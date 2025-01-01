using UnityEngine;
namespace Mirror.Examples.CouchCoop
{
    public class CameraViewForAll : MonoBehaviour
    {
        public Transform cameraTransform;
        public float camSpeed = 2.0f;
        public float orthoSizeSpeed = 2.0f;
        public Camera mainCamera;
        public float cameraZ = -5;

        public float cameraBufferX = 0.1f;
        public float cameraBufferY = 0.1f;
        public float minOrthographicSize = 0.1f;
        public float targetYPosition = 4.5f; // Optional Y position if cameras rotated

        private Vector2Int boundsMin;
        private Vector2Int boundsMax;
        private Vector3 targetCameraPosition;
        private float targetOrthographicSize;

        private void Update()
        {
            if (CouchPlayer.playersList.Count > 0)
            {
                CalculateBounds();
                CalculateTargetCameraPosAndSize();
                MoveCamera();
            }
        }

        private void CalculateBounds()
        {
            boundsMin = new Vector2Int(int.MaxValue, int.MaxValue);
            boundsMax = new Vector2Int(int.MinValue, int.MinValue);

            foreach (GameObject player in CouchPlayer.playersList)
            {
                Vector3 playerPosition = player.transform.position;
                boundsMin.x = Mathf.Min(boundsMin.x, Mathf.FloorToInt(playerPosition.x));
                boundsMin.y = Mathf.Min(boundsMin.y, Mathf.FloorToInt(playerPosition.y));
                boundsMax.x = Mathf.Max(boundsMax.x, Mathf.CeilToInt(playerPosition.x));
                boundsMax.y = Mathf.Max(boundsMax.y, Mathf.CeilToInt(playerPosition.y));
            }

            boundsMin.x -= Mathf.FloorToInt(cameraBufferX);
            boundsMin.y -= Mathf.FloorToInt(cameraBufferY);
            boundsMax.x += Mathf.CeilToInt(cameraBufferX);
            boundsMax.y += Mathf.CeilToInt(cameraBufferY);
        }

        private void CalculateTargetCameraPosAndSize()
        {
            float aspectRatio = (float)Screen.width / Screen.height;

            float requiredOrthographicSizeX = Mathf.Max((boundsMax.x - boundsMin.x) / 2 / aspectRatio, minOrthographicSize / aspectRatio);
            float requiredOrthographicSizeY = Mathf.Max(boundsMax.y - boundsMin.y / 2, minOrthographicSize);

            targetOrthographicSize = Mathf.Max(requiredOrthographicSizeX, requiredOrthographicSizeY);

            float cameraX = (boundsMax.x + boundsMin.x) / 2;
            float cameraY = targetYPosition != 0.0f ? targetYPosition : (boundsMax.y + boundsMin.y) / 2;

            targetCameraPosition = new Vector3(cameraX, cameraY, cameraZ);
        }

        private void MoveCamera()
        {
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetCameraPosition, camSpeed * Time.deltaTime);
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetOrthographicSize, orthoSizeSpeed * Time.deltaTime);
        }
    }
}