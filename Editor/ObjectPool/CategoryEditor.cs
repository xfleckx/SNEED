﻿using UnityEngine;
using UnityEditor;
using System.Linq;
using System;
using System.Collections.Generic;
using Assets.SNEED.Scripts.ObjectsAndCategories;
using Assets.SNEED.EditorExtensions.Util;

namespace Assets.SNEED.EditorExtensions.ObjectsAndCategories
{
    [CustomEditor(typeof(Category))]
    public class CategoryEditor : Editor
    {

        Category instance;

        private int currentPreviewIndex = 0;
        private GameObject currentPreviewObject;
        private string requestedObjectName;
        public override void OnInspectorGUI()
        {

            instance = target as Category;
            checkOnNullElements();

            lookupNewObjectsIn(instance);

            EditorGUILayout.BeginVertical();

            var objectCount = instance.AssociatedObjects.Count;

            if (objectCount == 0)
            {
                EditorGUILayout.HelpBox("No Objects in this category... Add them with the Object Pool Tools.", MessageType.Info);

                return;
            }

            GUILayout.Label(string.Format("Switch through {0} available objects", objectCount), EditorStyles.largeLabel);
             
            GUILayout.Label("Random sample a object");

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("With replacement"))
            {
                SetPreviewObject(instance.Sample());
            }

            if (GUILayout.Button("Without replacement"))
            {
                SetPreviewObject(instance.SampleWithoutReplacement());
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Get Object by it's name");

            requestedObjectName = EditorGUILayout.TextField(requestedObjectName);

            var instanceHasSuchAObject = instance.AssociatedObjects.Any((o) => o.name.Equals(requestedObjectName));

            if (instanceHasSuchAObject && GUILayout.Button("Get..."))
            {
                SetPreviewObject(instance.GetObjectBy(requestedObjectName));
            }

            EditorGUILayout.EndVertical();
        }

        private void checkOnNullElements()
        {

            if (instance.AssociatedObjects.Any(i => i == null))
            {
                var newListWithoutNullElements = new List<GameObject>();

                foreach (var item in instance.AssociatedObjects)
                {
                    if (item != null)
                        newListWithoutNullElements.Add(item);
                }
                instance.AssociatedObjects = newListWithoutNullElements;
            }
        }

        private void lookupNewObjectsIn(Category instance)
        {
            foreach (var child in instance.transform.AllChildren())
            {
                if (!instance.AssociatedObjects.Contains(child))
                {
                    instance.AssociatedObjects.Add(child);
                }
            }
        }

        void OnDisable()
        {
            if (currentPreviewObject != null)
                currentPreviewObject.SetActive(false);

            instance.AssociatedObjects.ForEach(o => o.SetActive(false));
        }

        private void SetPreviewObject(GameObject newPreview)
        {
            if (currentPreviewObject != null)
            {
                currentPreviewObject.SetActive(false);
            }

            currentPreviewObject = newPreview;

            currentPreviewObject.SetActive(true);
        }

        #region Custom Preview
        private PreviewRenderUtility _previewRenderUtility;
        private MeshFilter _targetMeshFilter;
        private MeshRenderer _targetMeshRenderer;
        private GameObject _currentObjectToPreview;

        private Vector2 _drag;
        private bool _previewAll;
        private int _lastIndex = 0;

        public override bool HasPreviewGUI()
        {
            if (_previewRenderUtility == null)
            {
                _previewRenderUtility = new PreviewRenderUtility();

                _previewRenderUtility.m_Camera.transform.position = new Vector3(0, 0, -6);
                _previewRenderUtility.m_Camera.transform.rotation = Quaternion.identity;
                
            }

            if (_currentObjectToPreview == null)
                _currentObjectToPreview = instance.AssociatedObjects.FirstOrDefault();

            return _currentObjectToPreview != null;
        }

        public override void OnPreviewSettings()
        {
            if (GUILayout.Button("Reset Camera", EditorStyles.whiteMiniLabel))
                _drag = Vector2.zero;


            _previewAll = GUILayout.Toggle(_previewAll, "Show All", EditorStyles.whiteMiniLabel);
                
        }
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            _drag = CodeUtils.Drag2D(_drag, r);

            var previewWidth = EditorGUIUtility.currentViewWidth - 10;

            if (!_previewAll) {
                
                EditorGUILayout.BeginVertical(GUILayout.Width(previewWidth));

                GUILayout.Label(string.Format("Show: {0}, {1}", currentPreviewIndex, _currentObjectToPreview.name), EditorStyles.whiteBoldLabel);

                EditorGUILayout.BeginHorizontal(GUILayout.Width(previewWidth));

                if (GUILayout.Button("Next"))
                {
                    int newIndex = (_lastIndex + 1) % instance.AssociatedObjects.Count;
                    _currentObjectToPreview = instance.AssociatedObjects[newIndex];
                    _lastIndex = newIndex;
                }

                if (GUILayout.Button("Previous"))
                {
                    var prev = ((_lastIndex - 1)) % instance.AssociatedObjects.Count;
                    int newIndex = prev >= 0 ? prev : instance.AssociatedObjects.Count + prev;
                    _currentObjectToPreview = instance.AssociatedObjects[newIndex];
                    _lastIndex = newIndex;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginVertical();

            }
            
            if (Event.current.type == EventType.Repaint)
            {
                if (!instance.AssociatedObjects.Any())
                {
                    EditorGUI.DropShadowLabel(r, "Has no objects!");
                }
                else
                {
                    if (!_previewAll)
                    {
                        var resultTexture = RenderObjectPreview(r, _currentObjectToPreview, background);

                        RenderObjectOrError(r, _currentObjectToPreview.name, resultTexture);
                    }
                    else
                    {
                        var previewGrid = EstimatePreviewGrid(r, instance.AssociatedObjects.Count);

                        int rows = previewGrid.GetLength(0);
                        int cols = previewGrid.GetLength(1);

                        var objectIndex = 0;

                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < cols; j++)
                            {
                                var currentObject = instance.AssociatedObjects[objectIndex];

                                var resultTexture = RenderObjectPreview(r, currentObject, background);
                                var currentPositionRect = previewGrid[i, j];
                                RenderObjectOrError(currentPositionRect, currentObject.name, resultTexture);
                                objectIndex++;
                            }
                        }
                    }
                }

            }
        }

        private Rect[,] EstimatePreviewGrid(Rect r, int count)
        {
            int rows = 1;
            int cols = 1;

            int modCount = count % 4;

            if (modCount == 0) { 
                cols = count / 2;
                rows = count / 2;
            }
            else
            {
                cols = (int)Math.Ceiling((decimal)count / 4);
                rows = (int)Math.Ceiling((decimal)count / 4);
            }
                

            float childRectWidth = r.width / cols;
            float childRectHeight = r.height / rows;

            var grid = new Rect[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    grid[i,j] = new Rect(r.x + j * childRectWidth, r.y + i * childRectHeight, childRectWidth, childRectHeight);
                }
            }

            return grid;
        }

        private void RenderObjectOrError(Rect r, string objectName, Texture resultTexture)
        {
            if (resultTexture != null)
                GUI.DrawTexture(r, resultTexture, ScaleMode.StretchToFill, false);
            else
            {
                var errorMessage = string.Format("Object {0} is missing", objectName);
                EditorGUI.HelpBox(r, errorMessage, MessageType.Error);
            }
        }

        private Texture RenderObjectPreview(Rect r, GameObject currentObjectToPreview, GUIStyle background)
        {
            var previewObject = GameObject.Instantiate(currentObjectToPreview);
            previewObject.hideFlags = HideFlags.HideAndDontSave;

            _targetMeshFilter = previewObject.GetComponentInChildren<MeshFilter>();
            _targetMeshRenderer = previewObject.GetComponentInChildren<MeshRenderer>();

            bool renderPreview = true;

            if (_targetMeshFilter == null || _targetMeshRenderer == null)
                renderPreview = false;

            if(renderPreview)
            {
                _previewRenderUtility.BeginPreview(r, background);

                var cam = _previewRenderUtility.m_Camera;
                cam.transform.position = Vector2.zero;
                cam.transform.rotation = Quaternion.Euler(new Vector3(-_drag.y, -_drag.x, 0));
                cam.transform.position = cam.transform.forward * -10f;

                cam.farClipPlane = 50;
                
                var lookAtCamRotation = previewObject.transform.rotation;

                _previewRenderUtility.DrawMesh(_targetMeshFilter.sharedMesh, Vector3.zero,
                // use the correction throught the Transform of the host gameobject
                lookAtCamRotation, _targetMeshRenderer.sharedMaterial, 0);

                var light = _previewRenderUtility.m_Light.First();
                light.type = LightType.Directional;
                light.transform.position = _previewRenderUtility.m_Camera.transform.position;
                light.transform.rotation = _previewRenderUtility.m_Camera.transform.rotation;

                _previewRenderUtility.m_Camera.Render();
                
                var result = _previewRenderUtility.EndPreview();

                DestroyImmediate(previewObject);

                return result;
            }

            return null;
        }
        #endregion
    }
}