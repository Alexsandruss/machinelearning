// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.DataView;
using Microsoft.ML.Auto;
using Microsoft.ML.CLI.CodeGenerator.CSharp;
using Microsoft.ML.CLI.Data;
using Microsoft.ML.CLI.Utilities;
using Microsoft.ML.Core.Data;
using Microsoft.ML.Data;
using NLog;

namespace Microsoft.ML.CLI.Commands.New
{
    internal class NewCommand : ICommand
    {
        private NewCommandOptions options;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        internal NewCommand(NewCommandOptions options)
        {
            this.options = options;
        }

        public void Execute()
        {
            var context = new MLContext();
            // Infer columns
            ColumnInferenceResults columnInference = InferColumns(context);

            Array.ForEach(columnInference.TextLoaderArgs.Column, t => t.Name = Sanitize(t.Name));

            // Load data
            (IDataView trainData, IDataView validationData) = LoadData(context, columnInference.TextLoaderArgs);

            // Explore the models
            (Pipeline, ITransformer) result = default;
            Console.WriteLine($"{Strings.ExplorePipeline}: {options.MlTask}");
            try
            {
                result = ExploreModels(context, trainData, validationData);
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Error, $"{Strings.ExplorePipelineException}:");
                logger.Log(LogLevel.Error, e.ToString());
                logger.Log(LogLevel.Error, Strings.Exiting);
                return;
            }

            //Get the best pipeline
            Pipeline pipeline = null;
            pipeline = result.Item1;
            var model = result.Item2;

            // Save the model
            logger.Log(LogLevel.Info, Strings.SavingBestModel);
            var modelPath = Path.Combine(@options.OutputBaseDir, options.OutputName);
            SaveModel(model, modelPath, $"{options.OutputName}_model.zip", context);

            // Generate the Project
            GenerateProject(columnInference, pipeline);
        }

        internal ColumnInferenceResults InferColumns(MLContext context)
        {
            //Check what overload method of InferColumns needs to be called.
            logger.Log(LogLevel.Info, Strings.InferColumns);
            ColumnInferenceResults columnInference = null;
            if (options.LabelName != null)
            {
                columnInference = context.AutoInference().InferColumns(options.TrainDataset.FullName, options.LabelName, groupColumns: false);
            }
            else
            {
                columnInference = context.AutoInference().InferColumns(options.TrainDataset.FullName, options.LabelIndex, groupColumns: false);
            }

            return columnInference;
        }

        internal void GenerateProject(ColumnInferenceResults columnInference, Pipeline pipeline)
        {
            //Generate code
            logger.Log(LogLevel.Info, Strings.GenerateProject);
            var codeGenerator = new CodeGenerator.CSharp.CodeGenerator(
                pipeline,
                columnInference,
                new CodeGeneratorOptions()
                {
                    TrainDataset = options.TrainDataset,
                    MlTask = options.MlTask,
                    TestDataset = options.TestDataset,
                    OutputName = options.OutputName,
                    OutputBaseDir = options.OutputBaseDir
                });
            codeGenerator.GenerateOutput();
        }

        internal (Pipeline, ITransformer) ExploreModels(MLContext context, IDataView trainData, IDataView validationData)
        {
            ITransformer model = null;
            string label = options.LabelName ?? "Label"; // It is guaranteed training dataview to have Label column
            Pipeline pipeline = null;

            if (options.MlTask == TaskKind.BinaryClassification)
            {
                var progressReporter = new ProgressHandlers.BinaryClassificationHandler();
                var result = context.AutoInference()
                    .CreateBinaryClassificationExperiment(new BinaryExperimentSettings()
                    {
                        MaxInferenceTimeInSeconds = options.Timeout,
                        ProgressCallback = progressReporter
                    })
                    .Execute(trainData, validationData, new ColumnInformation() { LabelColumn = label });
                logger.Log(LogLevel.Info, Strings.RetrieveBestPipeline);
                var bestIteration = result.Best();
                pipeline = bestIteration.Pipeline;
                model = bestIteration.Model;
            }

            if (options.MlTask == TaskKind.Regression)
            {
                var progressReporter = new ProgressHandlers.RegressionHandler();
                var result = context.AutoInference()
                    .CreateRegressionExperiment(new RegressionExperimentSettings()
                    {
                        MaxInferenceTimeInSeconds = options.Timeout,
                        ProgressCallback = progressReporter
                    }).Execute(trainData, validationData, new ColumnInformation() { LabelColumn = label });
                logger.Log(LogLevel.Info, Strings.RetrieveBestPipeline);
                var bestIteration = result.Best();
                pipeline = bestIteration.Pipeline;
                model = bestIteration.Model;
            }

            if (options.MlTask == TaskKind.MulticlassClassification)
            {
                throw new NotImplementedException();
            }
            //Multi-class exploration here

            return (pipeline, model);
        }

        internal (IDataView, IDataView) LoadData(MLContext context, TextLoader.Arguments textLoaderArgs)
        {
            logger.Log(LogLevel.Info, Strings.CreateDataLoader);
            var textLoader = context.Data.CreateTextLoader(textLoaderArgs);

            logger.Log(LogLevel.Info, Strings.LoadData);
            var trainData = textLoader.Read(options.TrainDataset.FullName);
            var validationData = options.ValidationDataset == null ? null : textLoader.Read(options.ValidationDataset.FullName);

            return (trainData, validationData);
        }

        internal static void SaveModel(ITransformer model, string ModelPath, string modelName, MLContext mlContext)
        {
            if (!Directory.Exists(ModelPath))
            {
                Directory.CreateDirectory(ModelPath);
            }
            ModelPath = Path.Combine(ModelPath, modelName);
            using (var fs = File.Create(ModelPath))
                model.SaveTo(mlContext, fs);
        }

        private static string Sanitize(string name)
        {
            return string.Join("", name.Select(x => Char.IsLetterOrDigit(x) ? x : '_'));
        }
    }
}
