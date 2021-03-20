using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UsingTheirs.RemoteInspector
{
    public class ExampleComponent : MonoBehaviour
    {
        private float rotationScale = 0;

        public int public_field_int;
        private int private_field_int;
        public int public_property_int { get; set; }
        public int private_property_int { get; set; }
        public void public_method()
        {
            Debug.Log("public_method called");
        }
        private void private_method()
        {
            Debug.Log("private called");
        }

        public List<int> public_field_list_int;

        public List<int> public_property_list_int
        {
            get { return public_field_list_int; }
            set { public_field_list_int = value; }
        }
        
        public string[] public_field_array_string;

        public string[] public_property_array_string
        {
            get { return public_field_array_string; }
            set
            {
                public_field_array_string = value; 
            }
        }

        public int property_error
        {
            get { throw new Exception("Property Exception Handler Test"); }
        }

        public Texture2D public_field_texture2D;
        public Texture3D public_field_texture3D;
        public Cubemap public_field_cubemap;
        public Texture2DArray public_field_texture2DArray;
        public CubemapArray public_field_cubemapArray;

        // Use this for initialization
        void Start()
        {

        }

        public bool reloadScene;
        public bool cloneForSpeedTest;

        // Update is called once per frame
        void Update()
        {
            transform.Rotate(new Vector3(0, rotationScale * Time.deltaTime, 0));

            if (reloadScene)
            {
                reloadScene = false;
                SceneManager.LoadScene("RemoteInspector/Example/Example");
            }

            if (cloneForSpeedTest)
            {
                cloneForSpeedTest = false;
                for (int i = 0; i < 1000; ++i)
                    Instantiate(gameObject);
            }
        }
    }
}
