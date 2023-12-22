using System.Collections.Generic;
using System.Linq;
using System.Text;
using HitScoreVisualizer.Extensions;
using HitScoreVisualizer.Settings;
using SiraUtil.Logging;
using TMPro;
using UnityEngine;

namespace HitScoreVisualizer.Services
{
	internal class JudgmentService
	{
		private readonly ConfigProvider _configProvider;
		private static SiraLog? _siraLog;

		public JudgmentService(ConfigProvider configProvider, SiraLog siraLog)
		{
			_configProvider = configProvider;
			_siraLog = siraLog;
		}

		internal void Judge(ScoreModel.NoteScoreDefinition noteScoreDefinition, ref TextMeshPro text, ref Color color, int score, int before, int after, int accuracy, float timeDependence)
		{
			var config = _configProvider.GetCurrentConfig();
			if (config == null)
			{
				return;
			}

			// enable rich text
			text.richText = true;
			// disable word wrap, make sure full text displays
			text.enableWordWrapping = false;
			text.overflowMode = TextOverflowModes.Overflow;

			// Selects the Judgment with a Threshold less than or equal to the score
			// Or the smallest Threshold if no such Judgment exists.
			var judgment = config.Judgments!
				.OrderByDescending(j => j.Threshold)
                .Where(j => j.Threshold <= score)
                .DefaultIfEmpty(config.Judgments.OrderByDescending(j => j.Threshold).Last())
                .First(); // Collection is guaranteed to have at least one element

			var index = config.Judgments!.IndexOf(judgment);

			if (judgment.Fade)
			{
				var fadeJudgment = index > 0 ? config.Judgments[index - 1] : judgment; // If there is no previous judgment, use the current one
				var baseColor = judgment.Color.ToColor();
				var fadeColor = fadeJudgment.Color.ToColor();
				var lerpDistance = judgment == fadeJudgment ? 0 : Mathf.InverseLerp(judgment.Threshold, fadeJudgment.Threshold, score);
				color = Color.Lerp(baseColor, fadeColor, lerpDistance);
			}
			else
			{
				color = judgment.Color.ToColor();
			}

			text.text = config.DisplayMode switch
			{
				"format" => DisplayModeFormat(noteScoreDefinition, score, before, after, accuracy, timeDependence, judgment, config),
				"textOnly" => judgment.Text,
				"numeric" => score.ToString(),
				"scoreOnTop" => $"{score}\n{judgment.Text}\n",
				_ => $"{judgment.Text}\n{score}\n"
			};
		}

		// ReSharper disable once CognitiveComplexity
		private static string DisplayModeFormat(ScoreModel.NoteScoreDefinition noteScoreDefinition, int score, int before, int after, int accuracy, float timeDependence, Judgment judgment, Configuration instance)
		{
			var formattedBuilder = new StringBuilder();
			var formatString = judgment.Text;
			var nextPercentIndex = formatString.IndexOf('%');
			while (nextPercentIndex != -1)
			{
				formattedBuilder.Append(formatString.Substring(0, nextPercentIndex));
				if (formatString.Length == nextPercentIndex + 1)
				{
					formatString += " ";
				}

				var specifier = formatString[nextPercentIndex + 1];

				switch (specifier)
				{
					case 'b':
						formattedBuilder.Append(before);
						break;
					case 'c':
						formattedBuilder.Append(accuracy);
						break;
					case 'a':
						formattedBuilder.Append(after);
						break;
					case 't':
						formattedBuilder.Append(ConvertTimeDependencePrecision(timeDependence, instance.TimeDependenceDecimalOffset, instance.TimeDependenceDecimalPrecision));
						break;
					case 'B':
						formattedBuilder.Append(JudgeSegment(before, instance.BeforeCutAngleJudgments));
						break;
					case 'C':
						formattedBuilder.Append(JudgeSegment(accuracy, instance.AccuracyJudgments));
						break;
					case 'A':
						formattedBuilder.Append(JudgeSegment(after, instance.AfterCutAngleJudgments));
						break;
					case 'T':
						formattedBuilder.Append(JudgeTimeDependenceSegment(timeDependence, instance.TimeDependenceJudgments, instance));
						break;
					case 's':
						formattedBuilder.Append(score);
						break;
					case 'p':
						formattedBuilder.Append($"{(double) score / noteScoreDefinition.maxCutScore * 100:0}");
						break;
					case '%':
						formattedBuilder.Append("%");
						break;
					case 'n':
						formattedBuilder.Append("\n");
						break;
					default:
						formattedBuilder.Append("%" + specifier);
						break;
				}

				formatString = formatString.Remove(0, nextPercentIndex + 2);
				nextPercentIndex = formatString.IndexOf('%');
			}

			return formattedBuilder.Append(formatString).ToString();
		}

		private static string JudgeSegment(int scoreForSegment, IList<JudgmentSegment>? judgments)
		{
			if (judgments == null)
			{
				return string.Empty;
			}

			foreach (var j in judgments)
			{
				if (scoreForSegment >= j.Threshold)
				{
					return j.Text ?? string.Empty;
				}
			}

			return string.Empty;
		}

		private static string JudgeTimeDependenceSegment(float scoreForSegment, IList<TimeDependenceJudgmentSegment>? judgments, Configuration instance)
		{
			if (judgments == null)
			{
				return string.Empty;
			}

			foreach (var j in judgments)
			{
				if (scoreForSegment >= j.Threshold)
				{
					return FormatTimeDependenceSegment(j, scoreForSegment, instance);
				}
			}

			// If no judgment is found, use the highest judgment
			return FormatTimeDependenceSegment(judgments.OrderByDescending(j => j.Threshold).First(), scoreForSegment, instance);
		}

		private static string FormatTimeDependenceSegment(TimeDependenceJudgmentSegment? judgment, float timeDependence, Configuration instance)
		{
			if (judgment == null)
			{
				return string.Empty;
			}

			var formattedBuilder = new StringBuilder();
			var formatString = judgment.Text ?? string.Empty;
			var nextPercentIndex = formatString.IndexOf('%');
			while (nextPercentIndex != -1)
			{
				formattedBuilder.Append(formatString.Substring(0, nextPercentIndex));
				if (formatString.Length == nextPercentIndex + 1)
				{
					formatString += " ";
				}

				var specifier = formatString[nextPercentIndex + 1];

				switch (specifier)
				{
					case 't':
						formattedBuilder.Append(ConvertTimeDependencePrecision(timeDependence, instance.TimeDependenceDecimalOffset, instance.TimeDependenceDecimalPrecision));
						break;
					case '%':
						formattedBuilder.Append("%");
						break;
					case 'n':
						formattedBuilder.Append("\n");
						break;
					default:
						formattedBuilder.Append("%" + specifier);
						break;
				}

				formatString = formatString.Remove(0, nextPercentIndex + 2);
				nextPercentIndex = formatString.IndexOf('%');
			}

			return formattedBuilder.Append(formatString).ToString();
		}

		private static string ConvertTimeDependencePrecision(float timeDependence, int decimalOffset, int decimalPrecision)
		{
			var multiplier = Mathf.Pow(10, decimalOffset);
			return (timeDependence * multiplier).ToString($"n{decimalPrecision}");
		}
	}
}