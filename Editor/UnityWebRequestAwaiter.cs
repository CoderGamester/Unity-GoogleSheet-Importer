using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.GoogleSheetImporter
{
	/// <summary>
	/// Allows to use <seealso cref="UnityWebRequest"/> with async/await mechanism
	/// </summary>
	public class UnityWebRequestAwaiter : INotifyCompletion
	{
		private readonly UnityWebRequestAsyncOperation _asyncOp;
		private Action _continuation;
		private bool _invoked;

		public UnityWebRequestAsyncOperation AsyncOperation => _asyncOp;
		
		private UnityWebRequestAwaiter() {}

		public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
		{
			_asyncOp = asyncOp;
			asyncOp.completed += OnRequestCompleted;
		}

		public bool IsCompleted => _asyncOp.isDone;

		public void GetResult() { }

		public void OnCompleted(Action continuation)
		{
			_continuation = continuation;

			if (_invoked)
			{
				_continuation();
			}
		}

		private void OnRequestCompleted(AsyncOperation obj)
		{
			_invoked = true;
			
			_continuation?.Invoke();
		}

		public static implicit operator UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
		{
			return new UnityWebRequestAwaiter(asyncOp);
		}

		public static implicit operator UnityWebRequestAsyncOperation(UnityWebRequestAwaiter awaiter)
		{
			return awaiter.AsyncOperation;
		}
	}

	public static class UnityWebRequestAwaiterExtension
	{
		public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
		{
			return new UnityWebRequestAwaiter(asyncOp);
		}
	}
}