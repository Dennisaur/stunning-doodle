using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using PDollarGestureRecognizer;

public class GestureHandlerQueue : MonoBehaviour {
	public Transform gestureOnScreenPrefab;
	public float gestureThreshold = 0.3f;
	public GameObject player;
	public GameObject hand;

	public GameObject gesture;
	public GestureHolder[] gestureHolders;

	#region Gesture variables
	private List<Gesture> trainingSet = new List<Gesture>();

	private List<Point> points = new List<Point>();
	private int strokeId = -1;

	private Vector3 virtualKeyPosition = Vector2.zero;
	private bool isDrawing = false;
	private Rect drawArea;

	private RuntimePlatform platform;
	private int vertexCount = 0;

	private List<LineRenderer> gestureLinesRenderer = new List<LineRenderer>();
	private LineRenderer currentGestureLineRenderer;
	#endregion

	//GUI
	private string message;
	private bool recognized;

	void Start () {

		platform = Application.platform;
		drawArea = new Rect(0, 150, Screen.width * 3, Screen.height * 4);

		// Load custom pre-made gestures
		TextAsset[] customGesturesXml = Resources.LoadAll<TextAsset>("CustomGestures/");
		foreach (TextAsset gestureXml in customGesturesXml)
			trainingSet.Add(GestureIO.ReadGestureFromXML(gestureXml.text));

		ResetGestures ();
	}

	void Update () {
		// Don't register gestures if paused
		if (GameManager.instance.GetIsPaused ()) {
			return;
		}

		if (platform == RuntimePlatform.Android || platform == RuntimePlatform.IPhonePlayer) {
			if (Input.touchCount > 0) {
				virtualKeyPosition = new Vector3 (Input.GetTouch (0).position.x, Input.GetTouch (0).position.y);
			} else if (isDrawing) {
				isDrawing = false;
				CompleteGesture ();
			}
		} else {
			if (Input.GetMouseButton(0)) {
				virtualKeyPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y);
			} else if (isDrawing) {
				isDrawing = false;
				CompleteGesture ();
			}
		}

		if (drawArea.Contains(virtualKeyPosition)) {
			if (Input.GetMouseButtonDown(0)) {

				isDrawing = true;

				++strokeId;
				
				Transform tmpGesture = Instantiate(gestureOnScreenPrefab, transform.position, transform.rotation) as Transform;
				currentGestureLineRenderer = tmpGesture.GetComponent<LineRenderer>();
				if (ThemeManager.instance.GetCurrentTheme ().name == "Space") {
					currentGestureLineRenderer.startColor = Color.white;
					currentGestureLineRenderer.endColor = Color.white;
				}
				
				gestureLinesRenderer.Add(currentGestureLineRenderer);
				
				vertexCount = 0;
			}
			
			if (Input.GetMouseButton(0)) {
				points.Add(new Point(virtualKeyPosition.x, -virtualKeyPosition.y, strokeId));

				currentGestureLineRenderer.positionCount = ++vertexCount;
				currentGestureLineRenderer.SetPosition(vertexCount - 1, Camera.main.ScreenToWorldPoint(new Vector3(virtualKeyPosition.x, virtualKeyPosition.y, 10)));
			}
		}
	}

	// For debugging
//	void OnGUI() {
//		GUI.Box(drawArea, "Draw Area");
//		GUI.Label(new Rect(10, Screen.height - 40, 500, 50), message);
//	}

	/// <summary>
	/// Resets all gestures.
	/// </summary>
	public void ResetGestures() {
		// Instantiate initial gestures
		for (int i = 0; i < gestureHolders.Length; i++) {
			CreateNewGesture ();
		}
	}

	/// <summary>
	/// Destroy existing gesture in gesture holder and create a new one
	/// </summary>
	/// <param name="gestureHolder">Gesture holder.</param>
	private void CreateNewGesture() {
		//TODO animate completed gesture

		// Destroy active gesture
		if (gestureHolders[0] != null) {
			Destroy (gestureHolders[0].gesture);
		}

		// Shift gestures
		for (int i = 0; i < gestureHolders.Length - 1; i++) {
			GestureHolder gestureHolder = gestureHolders [i];
			if (gestureHolders [i + 1].gesture) {
				gestureHolder.gesture = gestureHolders [i + 1].gesture;
				gestureHolder.gesture.transform.SetParent (gestureHolder.holder.transform);
				gestureHolder.gesture.GetComponent<RectTransform> ().anchoredPosition = Vector2.zero;
				gestureHolder.gesture.transform.localScale = ((i == 0) ? (new Vector3 (1f, 1f)) : (new Vector3(0.7f, 0.7f)));
			}
		}
		gestureHolders [gestureHolders.Length - 1].gesture = Instantiate (gesture, gestureHolders [gestureHolders.Length - 1].holder.transform);
	}

	/// <summary>
	/// Called when input released (gesture completed)
	/// </summary>
	void CompleteGesture() {
		// Get gesture result
		Gesture candidate = new Gesture(points.ToArray());
		Result gestureResult = PointCloudRecognizer.Classify(candidate, trainingSet.ToArray());

		message = gestureResult.GestureClass + " " + gestureResult.Score;

		CheckGesture(gestureResult);

		ClearDrawing ();
	}

	/// <summary>
	/// Compare gesture with recognizer
	/// </summary>
	/// <param name="gestureResult">Gesture result.</param>
	void CheckGesture(Result gestureResult) {

		if (gestureResult.Score > gestureThreshold) {
			// Check against current gesture
			GestureObject gestureObject = gestureHolders[0].holder.GetComponentInChildren<GestureObject> ();
			if (gestureResult.GestureClass == gestureObject.name) {
				// If auto game mode, move player to first animal position
				if (GameManager.instance.gameMode == GameMode.Auto) {
					float animalX = AnimalManager.instance.GetFirstAnimalX ();
					// Only spawn hand if animal exists
					if (animalX != Mathf.Infinity) {
						player.transform.position = new Vector3 (animalX, player.transform.position.y);
						AnimalManager.instance.AnimalsShift ();

						Instantiate (hand, player.transform.position, Quaternion.identity);
					}
				} else {
					// Spawn hand object at wherever player is located
					Instantiate (hand, player.transform.position, Quaternion.identity);
				}
				CreateNewGesture ();
			}
		}
	}

	/// <summary>
	/// Clears the line renderer
	/// </summary>
	void ClearDrawing() {
		strokeId = -1;

		points.Clear();

		foreach (LineRenderer lineRenderer in gestureLinesRenderer) {
			lineRenderer.positionCount = 0;
			Destroy(lineRenderer.gameObject);
		}

		gestureLinesRenderer.Clear();
	}
}

 // Used for setting corresponding gesture holder and spawn location in inspector
[System.Serializable]
public class GestureHolder {
	public GameObject holder;
	public GameObject spawnLocation;
	public GameObject gesture;
}