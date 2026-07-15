// <copyright file="OrderLabel.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Legacy.Maliev.DocumentService.Domain.OrderLabel
{
    /// <summary>
    /// Invoice Model.
    /// </summary>
    public class OrderLabel
    {

        /// <summary>
        /// Gets or sets the order id.
        /// </summary>
        /// <value>
        /// The order id.
        /// </value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the order name.
        /// </summary>
        /// <value>
        /// The order name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the order quantity.
        /// </summary>
        /// /// <value>
        /// The order quantity.
        /// </value>
        public int OrderQuantity { get; set; }

        /// <summary>
        /// Gets or sets the manufacture quantity.
        /// </summary>
        /// /// <value>
        /// The manufacture quantity.
        /// </value>
        public int ManufactureQuantity { get; set; }

        /// <summary>
        /// Gets or sets the remaining quantity.
        /// </summary>
        /// /// <value>
        /// The remaining quantity.
        /// </value>
        public int RemainingQuantity { get; set; }

        /// <summary>
        /// Gets or sets the order process.
        /// </summary>
        /// /// <value>
        /// The order process.
        /// </value>
        public string Process { get; set; }

        /// <summary>
        /// Gets or sets the order material.
        /// </summary>
        /// /// <value>
        /// The order material.
        /// </value>
        public string Material { get; set; }

        /// <summary>
        /// Gets or sets the order color.
        /// </summary>
        /// /// <value>
        /// The order color.
        /// </value>
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the order surface finish.
        /// </summary>
        /// /// <value>
        /// The order surface finish.
        /// </value>
        public string SurfaceFinish { get; set; }

        /// <summary>
        /// Gets or sets the order description.
        /// </summary>
        /// /// <value>
        /// The order description.
        /// </value>
        public string Description { get; set; }
    }
}