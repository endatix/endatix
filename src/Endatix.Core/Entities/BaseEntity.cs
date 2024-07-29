using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Endatix.Core.Entities;

public abstract class BaseEntity
{ [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public virtual long Id { get; set; }
  public DateTime CreatedAt { get; protected set; }
  public DateTime? ModifiedAt { get; protected set; }
}