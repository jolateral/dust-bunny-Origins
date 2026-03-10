using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ClothDynamics
{

	public class DynamicClothCreator : MonoBehaviour
	{
		public GameObject _absPrefab;
		public GameObject _clothPrefab;
		public GameObject _clothObject;
		public float _collidersFriction = 0.5f;

		void OnEnable()
		{
			var abs = this.gameObject.GetOrAddComponent<AutomaticBoneSpheres>();
			abs.GetCopyOfComponent(_absPrefab.GetComponent<AutomaticBoneSpheres>());
			abs.enabled = false;
			abs._spheresList.Clear();
			abs._roundConeList.Clear();
			abs._meshColliderSource.Clear();
			abs._meshColliderSource.AddRange(abs.GetComponentsInChildren<SkinnedMeshRenderer>().Select(x => x.gameObject));
			abs.enabled = true;

			var skinning = _clothObject.GetOrAddComponent<AutomaticSkinning>();
			var cloth = _clothObject.GetOrAddComponent<GPUClothDynamics>();
			cloth.enabled = false;
			cloth._runSim = false;

			if (_clothPrefab != null)
			{
				var comp = cloth.GetCopyOfComponent(_clothPrefab.GetComponent<GPUClothDynamics>()) as GPUClothDynamics;
				if (comp._useCollisionFinder)
				{
					var list = comp._meshObjects!= null ? comp._meshObjects.ToList() : new System.Collections.Generic.List<Transform>();
					list.AddRange(abs._meshColliderSource.Select(x => x.transform));
					comp._meshObjects = list.ToArray();
				}
				if (comp._useCollidableObjectsList)
				{
					cloth._collidableObjects.Clear();
					abs.ConvertToColliders(useCones: true, prompt: true);
					foreach (var item in cloth._collidableObjects)
					{
						item.GetOrAddComponent<ClothFrictionCollider>().friction = _collidersFriction;
					}
				}
			}
			cloth._runSim = true;
			cloth.enabled = true;
		}
	}

	public static class ComponentExtentions
	{
		public static T GetOrAddComponent<T>(this GameObject go) where T : Component
		{
			var comp = go.GetComponent<T>();
			if (comp == null) comp = go.AddComponent<T>();
			return comp as T;
		}

		public static T GetCopyOfComponent<T>(this Component comp, T other, bool NonPublic = false) where T : Component
		{
			Type type = comp.GetType();
			if (type != other.GetType()) return null; // type mis-match
			BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;
			if (NonPublic) flags |= BindingFlags.NonPublic;
			PropertyInfo[] pinfos = type.GetProperties(flags);
			foreach (var pinfo in pinfos)
			{
				if (pinfo.CanWrite)
				{
					try
					{
						pinfo.SetValue(comp, pinfo.GetValue(other, null), null);
					}
					catch { } // In case of NotImplementedException being thrown. For some reason specifying that exception didn't seem to catch it, so I didn't catch anything specific.
				}
			}
			FieldInfo[] finfos = type.GetFields(flags);
			foreach (var finfo in finfos)
			{
				finfo.SetValue(comp, finfo.GetValue(other));
			}
			return comp as T;
		}
	}
}