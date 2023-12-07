/*
© Siemens AG, 2017-2018
Author: Dr. Martin Bischoff (martin.bischoff@siemens.com)
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.IO;
using UnityEngine;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using Codice.Client.BaseCommands.Triggers;

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfAssetPathHandler
    {
        //Relative to Assets folder
        private static string packageRoot;
        private const string MaterialFolderName = "Materials";

        private static string OsPathSeparator
        {
            get
            {

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return ";";
                }
                return ":";
            }
        }

        public static IEnumerable<string> RosPackagePaths
        {
            get
            {
                String ros_paths = System.Environment.GetEnvironmentVariable("ROS_PACKAGE_PATH");

                IEnumerable<string> paths;

                if (ros_paths != null)
                {
                    return ros_paths.Split(OsPathSeparator);
                }
                
                // workaround for now if the workspace hasn't been sourced for the executable, e.g. launching from Unity Hub while testing
                // should probalby use regex that includes different directory seperators to stay OS agnostic
                // also requires arena-unity to be in the arena_ws
                // Todo: make fallback paths configurable
                paths = new List<string> { "/opt/ros/noetic/share/" };
                string dirPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (dirPath.Contains("src/arena-unity"))
                {
                    return paths.Append(dirPath[..dirPath.IndexOf("arena-unity")]);
                }
                return paths;
            }
        }

        #region SetAssetRootFolder
        public static void SetPackageRoot(string newPath, bool correctingIncorrectPackageRoot = false)
        {
            string oldPackagePath = packageRoot;

            packageRoot = GetRelativeAssetPath(newPath);

            if (!RuntimeUrdf.AssetDatabase_IsValidFolder(Path.Combine(packageRoot, MaterialFolderName)))
            {
                RuntimeUrdf.AssetDatabase_CreateFolder(packageRoot, MaterialFolderName);
            }

            if (correctingIncorrectPackageRoot)
            {
                MoveMaterialsToNewLocation(oldPackagePath);
            }
        }
        #endregion

        #region GetPaths
        public static string GetPackageRoot()
        {
            return packageRoot;
        }

        public static string GetRelativeAssetPath(string absolutePath)
        {
            string assetPath = absolutePath;
            var absolutePathUnityFormat = absolutePath.SetSeparatorChar();
            if (!absolutePathUnityFormat.StartsWith(Application.dataPath.SetSeparatorChar()))
            {
#if UNITY_EDITOR
                if (!RuntimeUrdf.IsRuntimeMode())
                {
                    if (absolutePath.Length > Application.dataPath.Length)
                    {
                        assetPath = absolutePath.Substring(Application.dataPath.Length - "Assets".Length);
                    }
                }
#endif
            }
            else
            {
                assetPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return assetPath.SetSeparatorChar();
        }

        public static string GetFullAssetPath(string relativePath)
        {
            string fullPath = Application.dataPath;
            if (relativePath.Substring(0, "Assets".Length) == "Assets")
            {
                fullPath += relativePath.Substring("Assets".Length);
            }
            else
            {
                fullPath = fullPath.Substring(0, fullPath.Length - "Assets".Length) + relativePath;
            }
            return fullPath.SetSeparatorChar();
        }

        public static string GetRelativeAssetPathFromUrdfPath(string urdfPath, bool convertToPrefab = true)
        {
            string path;
            bool useFileUri = false;
            if (!urdfPath.StartsWith(@"file://") && !urdfPath.StartsWith(@"package://"))
            {
                if (urdfPath.Substring(0, 3) == "../")
                {
                    UnityEngine.Debug.LogWarning("Attempting to replace file path's starting instance of `../` with standard package notation `package://` to prevent manual path traversal at root of directory!");
                    urdfPath = $@"package://{urdfPath.Substring(3)}";
                }
            }
            // loading assets relative path from ROS/ROS2 package.
            if (urdfPath.StartsWith(@"package://"))
            {
                path = urdfPath.Substring(10).SetSeparatorChar();

                //search through ROS package directories, if not found in unity repo
                if(!IsValidAssetPath(path)){
                    foreach(var packagePath in RosPackagePaths){
                        if(IsValidAssetPath(packagePath+path))
                            return packagePath+path;
                    }
                }
            }
            // loading assets from file:// type URI.
            else if (urdfPath.StartsWith(@"file://"))
            {
                path = urdfPath.Substring(7).SetSeparatorChar();
                useFileUri = true;
            }
            else
            {
                path = urdfPath.SetSeparatorChar();
            }

            if (convertToPrefab)
            {
                if (Path.GetExtension(path)?.ToLowerInvariant() == ".stl")
                    path = path.Substring(0, path.Length - 3) + "prefab";

            }
            if (useFileUri)
            {
                return path;
            }
            return Path.Combine(packageRoot, path);
        }
        #endregion

        public static bool IsValidAssetPath(string path)
        {
#if UNITY_EDITOR
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                return Directory.Exists(path) || File.Exists(path);
            }
#endif
            //RuntimeImporter. TODO: check if the path really exists
            return Directory.Exists(path) || File.Exists(path);
        }

        #region Materials

        private static void MoveMaterialsToNewLocation(string oldPackageRoot)
        {
            if (RuntimeUrdf.AssetDatabase_IsValidFolder(Path.Combine(oldPackageRoot, MaterialFolderName)))
            {
                RuntimeUrdf.AssetDatabase_MoveAsset(
                    Path.Combine(oldPackageRoot, MaterialFolderName),
                    Path.Combine(UrdfAssetPathHandler.GetPackageRoot(), MaterialFolderName));
            }
            else
            {
                RuntimeUrdf.AssetDatabase_CreateFolder(UrdfAssetPathHandler.GetPackageRoot(), MaterialFolderName);
            }
        }

        public static string GetMaterialAssetPath(string materialName)
        {
            return Path.Combine(packageRoot, MaterialFolderName, Path.GetFileName(materialName) + ".mat");
        }

        #endregion
    }

}