using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace IO.Swagger.Model {

  /// <summary>
  /// 
  /// </summary>
  [DataContract]
  public class Pagination {
    /// <summary>
    /// Current page number
    /// </summary>
    /// <value>Current page number</value>
    [DataMember(Name="number", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "number")]
    public int? Number { get; set; }

    /// <summary>
    /// Next page number
    /// </summary>
    /// <value>Next page number</value>
    [DataMember(Name="next_page_number", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "next_page_number")]
    public int? NextPageNumber { get; set; }

    /// <summary>
    /// Previous page number
    /// </summary>
    /// <value>Previous page number</value>
    [DataMember(Name="previous_page_number", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "previous_page_number")]
    public int? PreviousPageNumber { get; set; }

    /// <summary>
    /// Gets or Sets Paginator
    /// </summary>
    [DataMember(Name="paginator", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "paginator")]
    public Paginator Paginator { get; set; }

    /// <summary>
    /// If there is a next page
    /// </summary>
    /// <value>If there is a next page</value>
    [DataMember(Name="has_next", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "has_next")]
    public bool? HasNext { get; set; }

    /// <summary>
    /// If there is a previous page
    /// </summary>
    /// <value>If there is a previous page</value>
    [DataMember(Name="has_previous", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "has_previous")]
    public bool? HasPrevious { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class Pagination {\n");
      sb.Append("  Number: ").Append(Number).Append("\n");
      sb.Append("  NextPageNumber: ").Append(NextPageNumber).Append("\n");
      sb.Append("  PreviousPageNumber: ").Append(PreviousPageNumber).Append("\n");
      sb.Append("  Paginator: ").Append(Paginator).Append("\n");
      sb.Append("  HasNext: ").Append(HasNext).Append("\n");
      sb.Append("  HasPrevious: ").Append(HasPrevious).Append("\n");
      sb.Append("}\n");
      return sb.ToString();
    }

    /// <summary>
    /// Get the JSON string presentation of the object
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public string ToJson() {
      return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

}
}
