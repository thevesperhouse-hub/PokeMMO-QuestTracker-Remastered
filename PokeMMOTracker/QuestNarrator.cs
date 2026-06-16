using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PokeMMOTracker.Properties;
using SayIt;

namespace PokeMMOTracker;

// Quest narrator — neural Edge TTS (online) or Windows SAPI (offline fallback).
public static class QuestNarrator
{
	private static readonly object Lock = new object();
	private static SpeechSynthesizer _sapiSynth;
	private static MediaPlayer _mediaPlayer;
	private static bool _sapiInitFailed;
	private static bool _edgePlaying;
	private static string _lastVoiceLog = "";
	private static CancellationTokenSource _speakCts;
	private static readonly string TempAudioPath = Path.Combine(
		Path.GetTempPath(), "PokeMMOTracker_narrator.mp3");

	public static bool IsSpeaking
	{
		get
		{
			lock (Lock)
			{
				if (_edgePlaying) return true;
				return _sapiSynth != null && _sapiSynth.State == SynthesizerState.Speaking;
			}
		}
	}

	public static void Speak(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		Stop();
		_speakCts = new CancellationTokenSource();
		_ = SpeakAsync(text, _speakCts.Token);
	}

	public static void Stop()
	{
		try { _speakCts?.Cancel(); } catch { }

		lock (Lock)
		{
			try
			{
				if (_sapiSynth != null)
					_sapiSynth.SpeakAsyncCancelAll();
			}
			catch { }

			StopMediaPlayer();
		}
	}

	public static void Dispose()
	{
		Stop();
		lock (Lock)
		{
			try
			{
				if (_sapiSynth != null)
				{
					_sapiSynth.Dispose();
					_sapiSynth = null;
				}
			}
			catch { }
		}
	}

	private static async Task SpeakAsync(string text, CancellationToken ct)
	{
		if (Settings.Default.NarratorNeural)
		{
			try
			{
				await SpeakEdgeAsync(text, ct);
				return;
			}
			catch (Exception ex)
			{
				if (ct.IsCancellationRequested) return;
				TrackerLog.Error("QuestNarrator neural failed, fallback SAPI: " + ex.Message);
			}
		}

		if (!ct.IsCancellationRequested)
			SpeakSapi(text);
	}

	private static async Task SpeakEdgeAsync(string text, CancellationToken ct)
	{
		var config = new SayItConfig()
			.WithVoice(GetEdgeVoice())
			.WithRate("-6%");

		await global::SayIt.SayIt.SaveAsync(text, TempAudioPath, config, ct);
		if (ct.IsCancellationRequested) return;

		await Application.Current.Dispatcher.InvokeAsync(() =>
		{
			if (ct.IsCancellationRequested) return;

			lock (Lock)
			{
				StopMediaPlayer();
				_mediaPlayer = new MediaPlayer();
				_edgePlaying = true;
			}

			_mediaPlayer.MediaEnded += (_, _) =>
			{
				lock (Lock) { _edgePlaying = false; }
				StopMediaPlayer();
			};
			_mediaPlayer.MediaFailed += (_, _) =>
			{
				lock (Lock) { _edgePlaying = false; }
				StopMediaPlayer();
			};

			LogVoiceOnce("Neural: " + (AppConfig.Language == "FR" ? "fr-FR-DeniseNeural" : "en-US-JennyNeural"));
			_mediaPlayer.Open(new Uri(TempAudioPath, UriKind.Absolute));
			_mediaPlayer.Play();
		});
	}

	private static VoiceId GetEdgeVoice()
		=> AppConfig.Language == "FR" ? VoiceId.FrFRDeniseNeural : VoiceId.EnUSJennyNeural;

	private static void StopMediaPlayer()
	{
		try
		{
			if (_mediaPlayer != null)
			{
				_mediaPlayer.Stop();
				_mediaPlayer.Close();
				_mediaPlayer = null;
			}
		}
		catch { }
		_edgePlaying = false;
	}

	private static void SpeakSapi(string text)
	{
		lock (Lock)
		{
			if (_sapiInitFailed) return;
			try
			{
				EnsureSapiSynth();
				if (_sapiSynth == null) return;

				_sapiSynth.SpeakAsyncCancelAll();
				ApplyBestSapiVoice();
				_sapiSynth.Volume = 100;
				_sapiSynth.SpeakAsync(text);
			}
			catch (Exception ex)
			{
				TrackerLog.Error("QuestNarrator SAPI: " + ex.Message);
			}
		}
	}

	private static void EnsureSapiSynth()
	{
		if (_sapiSynth != null || _sapiInitFailed) return;
		try
		{
			_sapiSynth = new SpeechSynthesizer();
			_sapiSynth.SetOutputToDefaultAudioDevice();
		}
		catch (Exception ex)
		{
			_sapiInitFailed = true;
			TrackerLog.Error("QuestNarrator SAPI init failed: " + ex.Message);
		}
	}

	private static void ApplyBestSapiVoice()
	{
		if (_sapiSynth == null) return;

		string lang = AppConfig.Language == "FR" ? "fr" : "en";
		var voice = NarratorVoicePicker.PickSapi(_sapiSynth, lang);
		if (voice != null)
		{
			_sapiSynth.SelectVoice(voice.VoiceInfo.Name);
			_sapiSynth.Rate = NarratorVoicePicker.SapiRateFor(voice.VoiceInfo);
			LogVoiceOnce("Windows: " + voice.VoiceInfo.Name);
		}
		else
		{
			_sapiSynth.Rate = -2;
		}
	}

	private static void LogVoiceOnce(string voiceName)
	{
		if (_lastVoiceLog == voiceName) return;
		_lastVoiceLog = voiceName;
		TrackerLog.Info("Narrator voice -> " + voiceName);
	}
}
