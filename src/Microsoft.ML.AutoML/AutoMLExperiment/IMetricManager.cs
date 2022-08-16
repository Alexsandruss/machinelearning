﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.ML.AutoML
{
    /// <summary>
    /// Interface for metric manager.
    /// </summary>
    internal interface IMetricManager
    {
        bool IsMaximize { get; }
    }

    internal interface IEvaluateMetricManager : IMetricManager
    {
        double Evaluate(MLContext context, IDataView eval);

        string MetricName { get; }
    }

    internal class BinaryMetricManager : IEvaluateMetricManager
    {
        public BinaryMetricManager(BinaryClassificationMetric metric, string predictedColumn, string labelColumn)
        {
            Metric = metric;
            PredictedColumn = predictedColumn;
            LabelColumn = labelColumn;
        }

        public BinaryClassificationMetric Metric { get; set; }

        public string PredictedColumn { get; set; }

        public string LabelColumn { get; set; }

        public bool IsMaximize => Metric switch
        {
            BinaryClassificationMetric.Accuracy => true,
            BinaryClassificationMetric.AreaUnderPrecisionRecallCurve => true,
            BinaryClassificationMetric.AreaUnderRocCurve => true,
            BinaryClassificationMetric.PositivePrecision => true,
            BinaryClassificationMetric.NegativePrecision => true,
            BinaryClassificationMetric.NegativeRecall => true,
            BinaryClassificationMetric.PositiveRecall => true,
            BinaryClassificationMetric.F1Score => true,
            _ => throw new NotImplementedException(),
        };

        public string MetricName => Metric.ToString();

        public double Evaluate(MLContext context, IDataView eval)
        {
            var metric = context.BinaryClassification.EvaluateNonCalibrated(eval, labelColumnName: LabelColumn, predictedLabelColumnName: PredictedColumn);

            return Metric switch
            {
                BinaryClassificationMetric.Accuracy => metric.Accuracy,
                BinaryClassificationMetric.AreaUnderPrecisionRecallCurve => metric.AreaUnderPrecisionRecallCurve,
                BinaryClassificationMetric.AreaUnderRocCurve => metric.AreaUnderRocCurve,
                BinaryClassificationMetric.PositivePrecision => metric.PositivePrecision,
                BinaryClassificationMetric.NegativePrecision => metric.NegativePrecision,
                BinaryClassificationMetric.NegativeRecall => metric.NegativeRecall,
                BinaryClassificationMetric.PositiveRecall => metric.PositivePrecision,
                BinaryClassificationMetric.F1Score => metric.F1Score,
                _ => throw new NotImplementedException(),
            };
        }
    }

    internal class MultiClassMetricManager : IMetricManager
    {
        public MulticlassClassificationMetric Metric { get; set; }

        public string PredictedColumn { get; set; }

        public string LabelColumn { get; set; }

        public bool IsMaximize => Metric switch
        {
            MulticlassClassificationMetric.MacroAccuracy => true,
            MulticlassClassificationMetric.MicroAccuracy => true,
            MulticlassClassificationMetric.LogLoss => false,
            MulticlassClassificationMetric.LogLossReduction => false,
            MulticlassClassificationMetric.TopKAccuracy => true,
            _ => throw new NotImplementedException(),
        };
    }

    internal class RegressionMetricManager : IMetricManager
    {
        public RegressionMetric Metric { get; set; }

        public string ScoreColumn { get; set; }

        public string LabelColumn { get; set; }

        public bool IsMaximize => Metric switch
        {
            RegressionMetric.RSquared => true,
            RegressionMetric.RootMeanSquaredError => false,
            RegressionMetric.MeanSquaredError => false,
            RegressionMetric.MeanAbsoluteError => false,
            _ => throw new NotImplementedException(),
        };
    }
}
