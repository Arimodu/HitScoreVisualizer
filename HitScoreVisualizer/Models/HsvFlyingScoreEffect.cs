using HitScoreVisualizer.Services;
using HitScoreVisualizer.Settings;
using SiraUtil.Affinity;
using UnityEngine;
using Zenject;

namespace HitScoreVisualizer.Models
{
	public class HsvFlyingScoreEffectPatchHooks : IAffinity
	{
		[AffinityPrefix]
		[AffinityPatch(typeof(FlyingScoreEffect), nameof(FlyingScoreEffect.InitAndPresent))]
		internal bool InitAndPresentReplacement(FlyingScoreEffect __instance, IReadonlyCutScoreBuffer cutScoreBuffer, float duration, Vector3 targetPos, Color color)
		{
			if (__instance is HsvFlyingScoreEffect hsvInstance)
			{
				hsvInstance.InitAndPresent(cutScoreBuffer, duration, targetPos, color);
				return false;
			}

			return true;
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(FlyingScoreEffect), nameof(FlyingScoreEffect.HandleCutScoreBufferDidChange))]
		internal bool HandleCutScoreBufferDidChangeReplacement(FlyingScoreEffect __instance, CutScoreBuffer cutScoreBuffer)
		{
			if (__instance is HsvFlyingScoreEffect hsvInstance)
			{
				hsvInstance.HandleCutScoreBufferDidChange(cutScoreBuffer);
				return false;
			}

			return true;
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(FlyingScoreEffect), nameof(FlyingScoreEffect.HandleCutScoreBufferDidFinish))]
		internal bool HandleCutScoreBufferDidFinishReplacement(FlyingScoreEffect __instance, CutScoreBuffer cutScoreBuffer)
		{
			if (__instance is HsvFlyingScoreEffect hsvInstance)
			{
				hsvInstance.HandleCutScoreBufferDidFinish(cutScoreBuffer);
				return false;
			}
			return true;
		}
	}

	internal sealed class HsvFlyingScoreEffect : FlyingScoreEffect
	{
		private JudgmentService _judgmentService = null!;
		private Configuration? _configuration;

		[Inject]
		internal void Construct(JudgmentService judgmentService, ConfigProvider configProvider)
		{
			_judgmentService = judgmentService;
			_configuration = configProvider.GetCurrentConfig();
		}

		public new void InitAndPresent(IReadonlyCutScoreBuffer cutScoreBuffer, float duration, Vector3 targetPos, Color color)
		{
			if (_configuration != null)
			{
				if (_configuration.FixedPosition != null)
				{
					// Set current and target position to the desired fixed position
					targetPos = _configuration.FixedPosition.Value;
					transform.position = targetPos;
				}
				else if (_configuration.TargetPositionOffset != null)
				{
					targetPos += _configuration.TargetPositionOffset.Value;
				}
			}

			_color = color;
			_cutScoreBuffer = cutScoreBuffer;
			if (!cutScoreBuffer.isFinished)
			{
				cutScoreBuffer.RegisterDidChangeReceiver(this);
				cutScoreBuffer.RegisterDidFinishReceiver(this);
				_registeredToCallbacks = true;
			}

			if (_configuration == null)
			{
				_text.text = cutScoreBuffer.cutScore.ToString();
				_maxCutDistanceScoreIndicator.enabled = cutScoreBuffer.centerDistanceCutScore == cutScoreBuffer.noteScoreDefinition.maxCenterDistanceCutScore;
				_colorAMultiplier = cutScoreBuffer.cutScore > cutScoreBuffer.maxPossibleCutScore * 0.9f ? 1f : 0.3f;
			}
			else
			{
				_maxCutDistanceScoreIndicator.enabled = false;

				// Apply judgments a total of twice - once when the effect is created, once when it finishes.
				Judge((CutScoreBuffer) cutScoreBuffer, 30);
			}

			InitAndPresent(duration, targetPos, cutScoreBuffer.noteCutInfo.worldRotation, false);
		}

		public override void ManualUpdate(float t)
		{
			var c = _color;
			var color = new Color(c.r, c.g, c.b, _fadeAnimationCurve.Evaluate(t));
			_text.color = color;
			_maxCutDistanceScoreIndicator.color = color;
		}

		public new void HandleCutScoreBufferDidChange(CutScoreBuffer cutScoreBuffer)
		{
			if (_configuration == null)
			{
				base.HandleCutScoreBufferDidChange(cutScoreBuffer);
				return;
			}

			if (_configuration.DoIntermediateUpdates)
			{
				Judge(cutScoreBuffer);
			}
		}

		public new void HandleCutScoreBufferDidFinish(CutScoreBuffer cutScoreBuffer)
		{
			if (_configuration != null)
			{
				Judge(cutScoreBuffer);
			}

			base.HandleCutScoreBufferDidFinish(cutScoreBuffer);
		}

		private void Judge(IReadonlyCutScoreBuffer cutScoreBuffer, int? assumedAfterCutScore = null)
		{
			_judgmentService.Judge(cutScoreBuffer, ref _text, ref _color, assumedAfterCutScore);
		}
	}
}