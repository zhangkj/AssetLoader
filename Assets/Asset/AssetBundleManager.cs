﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public enum LoadType
{
    Sync,
    Async,
}

public class AssetBundleManager  {

    #region GetSingleton
    private static AssetBundleManager mInstance;
    public static AssetBundleManager GetSingleton()
    {
        if(mInstance == null)
        {
            mInstance = new AssetBundleManager();
        }
        return mInstance;
    }
    #endregion

    AssetBundle mManifestAssetBundle;
	AssetBundleManifest mManifest;
	Dictionary<string, AssetBundleEntity> mAssetBundleDic = new Dictionary<string, AssetBundleEntity>();

		
	Queue<AssetBundleLoadTask> mAssetBundleLoadTaskQueue = new Queue<AssetBundleLoadTask>();

   public int assetMode { get; private set; }

    public LoadType loadType = LoadType.Async;

    public void Init(string varAssetManifestName)
	{
        string tmpAssetManifest = GetAssetBundlePath() + varAssetManifestName;
		if (File.Exists (tmpAssetManifest)) {

			mManifestAssetBundle = AssetBundle.LoadFromFile (tmpAssetManifest);
		
			if (mManifestAssetBundle) {
			
				mManifest = mManifestAssetBundle.LoadAsset ("AssetBundleManifest") as AssetBundleManifest;

			    UnityEngine.Object.DontDestroyOnLoad (mManifest);
			
			}
		}

        assetMode = PlayerPrefs.GetInt("assetmode");
    }


	public void Update()
	{
		if (mAssetBundleLoadTaskQueue.Count > 0) {

			AssetBundleLoadTask tmpLoadTask = mAssetBundleLoadTaskQueue.Peek ();

			if (tmpLoadTask == null) 
			{
                mAssetBundleLoadTaskQueue.Dequeue ();

				return;
			} 
			else
			{
				string tmpAssetBundleName = tmpLoadTask.assetBundleName.ToLower ();

				if (mAssetBundleDic.ContainsKey (tmpAssetBundleName))
				{
					Debug.Log("Loaded:"+tmpLoadTask.assetBundleName);

					tmpLoadTask.state = LoadStatus.Finish;
                   
                    tmpLoadTask.LoadFinish(mAssetBundleDic[tmpAssetBundleName]);

                    mAssetBundleLoadTaskQueue.Dequeue ();


                    return;
				}

				if (tmpLoadTask.state == LoadStatus.Cancel||
                    tmpLoadTask.state == LoadStatus.Error
                    )
                {
                    mAssetBundleLoadTaskQueue.Dequeue ();
					return;
				}
				else if (tmpLoadTask.state == LoadStatus.Wait) 
				{
                    if (loadType == LoadType.Sync)
                    {
                        tmpLoadTask.LoadSync();
                    }
                    else
                    {
                        tmpLoadTask.LoadAsync();
                    }
					//Debug.Log("Start Load:"+tmpLoadTask.mAssetBundleName);

					return;
				} 
				else if (tmpLoadTask.state ==LoadStatus.Loading) 
				{
                    tmpLoadTask.CheckLoadAsync();

					return;
				} 
				else if (tmpLoadTask.state == LoadStatus.Finish)
				{
					
                    OnAssetBundleLoadTaskFinish(tmpLoadTask);

                    mAssetBundleLoadTaskQueue.Dequeue ();

					//Debug.Log ("LoadTask Count:" + mLoadingAssetQueue.Count);
				}
			}
		}
	}

    public AssetBundleLoadTask GetLoadTask(string varAssetBundleName)
    {
        var it = mAssetBundleLoadTaskQueue.GetEnumerator();
      
        while(it.MoveNext())
        {
            if(it.Current.assetBundleName == varAssetBundleName)
            {

                return it.Current;
            }
        }
        return null;
    }

    public string[] GetAllDependencies(string varAssetBundleName)
    {
        if(string.IsNullOrEmpty(varAssetBundleName) || mManifest == null)
        {
            return null;
        }

        return mManifest.GetAllDependencies(varAssetBundleName);

    }


    private void OnAssetBundleLoadTaskFinish(AssetBundleLoadTask varLoadTask)
    {
        if (varLoadTask == null) { return; }

        string tmpAssetBundleName = varLoadTask.assetBundleName.ToLower();

        if (mAssetBundleDic.ContainsKey(tmpAssetBundleName) == false)
        {
            AssetBundle tmpAssetBundle = varLoadTask.assetBundle;

            AssetBundleEntity tmpBundleEntity = new AssetBundleEntity(tmpAssetBundleName, tmpAssetBundle);

            mAssetBundleDic.Add(tmpAssetBundleName, tmpBundleEntity);
        }
      
        varLoadTask.LoadFinish(mAssetBundleDic[tmpAssetBundleName]);

    }

    public AssetBundleLoadTask Load(string varAssetBundleName,System.Action<AssetBundleEntity> varCallback)
    {
        string tmpAssetBundleName = varAssetBundleName.ToLower();

        AssetBundleLoadTask tmpLoadTask = null;
 
        if (mAssetBundleDic.ContainsKey(varAssetBundleName))
        {
            if (varCallback != null)
            {
                AssetBundleEntity tmpBundleEntity = mAssetBundleDic[tmpAssetBundleName];

                varCallback(tmpBundleEntity);
            }

            tmpLoadTask = new AssetBundleLoadTask(tmpAssetBundleName);

            return tmpLoadTask;
        }

        tmpLoadTask = GetLoadTask(tmpAssetBundleName);

        if (tmpLoadTask != null)
        {
            tmpLoadTask.AddAssetLoadTask(varAssetBundleName, varCallback);
            return tmpLoadTask;
        }

        string[] tmpDependences = GetAllDependencies(varAssetBundleName);
        if (tmpDependences != null)
        {
            for (int i = 0; i < tmpDependences.Length; ++i)
            {
                string tmpDependentAssetBundleName = tmpDependences[i].ToLower();

                if (!mAssetBundleDic.ContainsKey(tmpDependentAssetBundleName) && GetLoadTask(tmpDependentAssetBundleName) == null)
                {
                    AssetBundleLoadTask tmpDependenceLoadTask = new AssetBundleLoadTask(tmpDependentAssetBundleName);

                    mAssetBundleLoadTaskQueue.Enqueue(tmpDependenceLoadTask);
                }
            }
        }

        tmpLoadTask = new AssetBundleLoadTask(tmpAssetBundleName);

        tmpLoadTask.AddAssetLoadTask(varAssetBundleName, varCallback);

        mAssetBundleLoadTaskQueue.Enqueue(tmpLoadTask);

        return tmpLoadTask;
    }


	public AssetBundleEntity GetAssetBundleEntity(string varAssetbundleName)
	{
		AssetBundleEntity tmpBundleEntity = null;

		mAssetBundleDic.TryGetValue (varAssetbundleName.ToLower(), out  tmpBundleEntity);

		return tmpBundleEntity;
	}

	public void LoadScene(string varAssetBundleName,System.Action varCallback)
	{
        Load(varAssetBundleName,  (varGo) => {

            if(varCallback!=null)
            {
                varCallback();
            }
        });
	}

	IEnumerator LoadSceneAsync(string varAssetBundleName,System.Action varCallback)
	{
		string tmpAssetBundleName = varAssetBundleName.ToLower ();

		if (mAssetBundleDic.ContainsKey (tmpAssetBundleName)) {

			if (varCallback != null) {
				varCallback ();
			}

			yield break;
		
		}
		string tmpScenePath = GetAssetBundlePath () + varAssetBundleName;
		using(WWW www = new WWW ("file://" + tmpScenePath))
		{
			yield return www;

			if (www.isDone) {

				//if (www.assetBundle != null) {
					
				//}
				//mAssetBundleDic.Add (tmpAssetBundleName, new LoadedAssetbundle (varAssetBundleName, AssetType.Scene, www.assetBundle));

				if (varCallback != null) {
					varCallback ();
				}
			}
		}
	}

 


    public void UnLoad(AssetBundleEntity varBundleEntity)
	{
        if(varBundleEntity == null)
        {
            return;
        }
        Debug.Log("UnLoad:"+ varBundleEntity.assetBundleName);

        string tmpAssetBundleName = varBundleEntity.assetBundleName.ToLower();
	
        //先移除
		mAssetBundleDic.Remove (tmpAssetBundleName);

        //卸载本身以及单独的依赖
        varBundleEntity.UnLoad(); 
    }

    public bool OtherDependence(string varAssetBundleName)
    {
        var it = mAssetBundleDic.GetEnumerator();
        while (it.MoveNext())
        {
            if (it.Current.Value.Dependence(varAssetBundleName))
            {

                return true;
            }
        }
        return false;
    }


	public static string GetAssetBundlePath()
	{
		return Application.dataPath +"/../StreamingAssets/";
	}
   

	public void Destroy()
	{
        string[] tmpAssetBundleArray = new string[mAssetBundleDic.Count];
        mAssetBundleDic.Keys.CopyTo(tmpAssetBundleArray, 0);
        for (int i = 0,  max = tmpAssetBundleArray.Length; i <max; ++i)
        {
            AssetBundleEntity tmpBundleEntity = GetAssetBundleEntity(tmpAssetBundleArray[i]);
            UnLoad(tmpBundleEntity);
        }

        Array.Clear(tmpAssetBundleArray, 0, tmpAssetBundleArray.Length);
        tmpAssetBundleArray = null;

        mAssetBundleDic.Clear ();

		if (mManifestAssetBundle) {
			mManifestAssetBundle.Unload (true);
		}
	}
}