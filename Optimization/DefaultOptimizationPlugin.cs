using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using RightEdge.Common;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace RightEdge.Optimization
{
	[DisplayName("Default (Brute Force) Optimization Plugin")]
	public class DefaultOptimizationPlugin : OptimizationPlugin
	{
		[Description("The number of threads to use for optimization, or 0 if the plugin should try to use the appropriate number of threads for the number of cores in the machine.")]
		public int ThreadsToUse { get; set; }

		protected override bool ShowOptimizationSettings(SystemRunSettings runSettings, System.Windows.Forms.IWin32Window owner)
		{
			return base.ShowOptimizationSettings(runSettings, owner);
		}

		protected override void LoadOptimizationSettingsFromFile(SystemRunSettings runSettings, string filename)
		{
			base.LoadOptimizationSettingsFromFile(runSettings, filename);
		}

		class OptimizationRunItem
		{
			public int RunNumber { get; set; }
			public List<KeyValuePair<string, double>> ParameterValues { get; set; }
			public OptimizationResult Results { get; set; }
		}


		List<OptimizationRunItem> CreateRunItems(SystemRunSettings settings)
		{
			List<OptimizationRunItem> ret = new List<OptimizationRunItem>();
			int[] optProgress = new int[OptimizationParameters.Count];
			int runNo = 0;
			while (true)
			{
				Dictionary<string, double> paramDict = new Dictionary<string, double>(OptimizationParameters.Count);

				runNo++;

				for (int i = 0; i < OptimizationParameters.Count; i++)
				{
					SystemParameterInfo param = OptimizationParameters[i];
					double currentValue;
					if (param.NumSteps > 1)
					{
						double stepSize = (double)((Decimal)(param.High - param.Low) / (Decimal)(param.NumSteps - 1));
						currentValue = param.Low + optProgress[i] * stepSize;
					}
					else
					{
						currentValue = param.Low;
					}
					paramDict[param.Name] = currentValue;
				}

				var item = new OptimizationRunItem();
				item.RunNumber = runNo;
				item.ParameterValues = paramDict.ToList();

				ret.Add(item);

				bool done = true;
				for (int i = 0; i < optProgress.Length; i++)
				{
					optProgress[i]++;
					if (optProgress[i] < OptimizationParameters[i].NumSteps)
					{
						done = false;
						break;
					}
					else
					{
						optProgress[i] = 0;
					}
				}

				if (done)
				{
					break;
				}
			}
			return ret;
		}

		Action _cancelAction = null;

		public override List<OptimizationResult> RunOptimization(SystemRunSettings runSettings)
		{
			Stopwatch optimizationTime = new Stopwatch();
			optimizationTime.Start();

            var runItems = CreateRunItems(runSettings);

			int numThreads = this.ThreadsToUse;
			if (numThreads == 0)
			{
                numThreads = Math.Min(Environment.ProcessorCount, runItems.Count);
			}

			object lockObject = new object();
			ManualResetEvent[] doneEvents = new ManualResetEvent[numThreads];
			for (int i = 0; i < numThreads; i++)
			{
				doneEvents[i] = new ManualResetEvent(false);
			}

			bool cancelled = false;
			_cancelAction = delegate
			{
				lock (lockObject)
				{
					cancelled = true;
				}
			};


			
			int nextItem = 0;
			int completedRuns = 0;
			List<ProgressItem> progressItems = Enumerable.Range(0, numThreads + 1).Select(i => new ProgressItem("Initializing...", 0)).ToList();
			List<string> progressDebug = progressItems.Select(i => "").ToList();

			List<Exception> exceptions = new List<Exception>();
			


			WaitCallback worker = arg =>
			{
				int workerIndex = (int)arg;
				try
				{
					while (true)
					{
						OptimizationRunItem runItem;
						SystemRunSettings settings;
						lock (lockObject)
						{
							if (nextItem >= runItems.Count)
							{
								break;
							}

							if (cancelled)
							{
								throw new OperationCanceledException();
							}

							runItem = runItems[nextItem];
							nextItem++;
							settings = runSettings.Clone();

							progressItems[workerIndex + 1] = new ProgressItem("Initializing run " + runItem.RunNumber + "...", 0);
							progressDebug[workerIndex + 1] = workerIndex.ToString() + ": Initializing";
							UpdateProgress(progressItems);
						}

						settings.SystemParameters = runItem.ParameterValues;
						settings.RunNumber = runItem.RunNumber;

						runItem.Results = RunSystem(settings, (currentItem, totalItems, currentTime) =>
						{
							double currentRunProgress = (double)currentItem / totalItems;
							string currentRunProgressText = null;
							if (currentTime != DateTime.MinValue)
							{
								CultureInfo culture = BarUtils.GetCurrencyCulture(settings.AccountCurrency);
								currentRunProgressText = "Run " + runItem.RunNumber + " progress: " + currentTime.ToString(culture.DateTimeFormat);
							}

							ProgressItem progressItem;
							try
							{
								progressItem = new ProgressItem(currentRunProgressText, currentRunProgress);
							}
							catch (ArgumentOutOfRangeException ex)
							{
								string msg = "Invalid current run progress.  Current item: " + currentItem + " Total items: " + totalItems + " Current run progress: " + currentRunProgress;
								throw new ArgumentOutOfRangeException(msg, ex);
							}

							lock (lockObject)
							{
								if (cancelled)
								{
									throw new OperationCanceledException();
								}

								progressItems[workerIndex + 1] = progressItem;
								progressDebug[workerIndex + 1] = workerIndex.ToString() + ": " + currentItem + "/" + totalItems;

								string overallProgressText = string.Format("Using {2} threads.  {0} of {1} runs completed.", completedRuns, runItems.Count, numThreads);
								double overallProgress = completedRuns / (double)runItems.Count;
								overallProgress += progressItems.Skip(1).Sum(item => item.Progress) / runItems.Count;
								//	Floating point rounding could cause overallProgress to be greater than 1
								if (overallProgress > 1.0 && overallProgress < 1.1)
								{
									overallProgress = 1;
								}

								progressDebug[0] = "Overall: " + overallProgress;

								if (overallProgress > 1.0)
								{
									StringBuilder sb = new StringBuilder();
									sb.AppendLine("Overall progress over 100%: " + overallProgress);
									sb.AppendLine("Completed " + completedRuns + " out of " + runItems.Count + " runs.");
									foreach (var dbg in progressDebug)
									{
										Debug.WriteLine(dbg);
										sb.AppendLine(dbg);
									}

									throw new RightEdgeError(sb.ToString());
								}

								progressItems[0] = new ProgressItem(overallProgressText, overallProgress);
								

								UpdateProgress(progressItems);
							}
						});

						runItem.Results.RunNumber = runItem.RunNumber;

						if (!settings.SaveOptimizationResults)
						{
							File.Delete(runItem.Results.ResultsFile);
						}

						lock (lockObject)
						{
							completedRuns++;
							progressItems[workerIndex + 1] = new ProgressItem("Run " + runItem.RunNumber + " complete.", 0);
							progressDebug[workerIndex + 1] = workerIndex.ToString() + ": Complete";
						}
					}
				}
				catch (Exception ex)
				{
					if (!(ex is OperationCanceledException))
					{
						lock (lockObject)
						{
							exceptions.Add(ex);
						}
					}
				}
				finally
				{
					((ManualResetEvent)doneEvents[workerIndex]).Set();
				}
			};

			for (int i = 0; i < numThreads; i++)
			{
				ThreadPool.QueueUserWorkItem(worker, i);
			}

			WaitHandle.WaitAll(doneEvents);

			if (cancelled)
			{
				throw new OperationCanceledException();
			}

			if (exceptions.Any())
			{
				throw new RightEdgeError("Exception during optimization.", exceptions.First());
			}

			var optimizationResults = runItems.Select(i => i.Results).ToList();

			//	Remove parameters that weren't optimized from the list so that they don't show up as columns in the optimization results
			HashSet<string> unoptimizedParameters = new HashSet<string>(OptimizationParameters.Where(p => p.NumSteps <= 1).Select(p => p.Name));
			foreach (var result in optimizationResults)
			{
				result.ParameterValues = result.ParameterValues.Where(p => !unoptimizedParameters.Contains(p.Key)).ToList();
			}

			_cancelAction = null;

			optimizationTime.Stop();
			Debug.WriteLine("Optimization time: " + optimizationTime.Elapsed.ToString());

			return optimizationResults;
		}

		protected override void CancelOptimization()
		{
			var cancelAction = _cancelAction;
			if (_cancelAction != null)
			{
				_cancelAction();
			}
			else
			{
				base.CancelOptimization();
			}
		}
	}
}
