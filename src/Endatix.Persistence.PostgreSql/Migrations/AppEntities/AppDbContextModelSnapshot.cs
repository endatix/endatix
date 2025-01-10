﻿// <auto-generated />
using System;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Endatix.Persistence.PostgreSQL.Migrations.AppEntities
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Endatix.Core.Entities.Form", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<long?>("ActiveDefinitionId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("ActiveDefinitionId")
                        .IsUnique();

                    b.ToTable("Forms", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormDefinition", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("FormId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsDraft")
                        .HasColumnType("boolean");

                    b.Property<string>("JsonData")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("FormId");

                    b.ToTable("FormDefinitions", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.Submission", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("CurrentPage")
                        .HasColumnType("integer");

                    b.Property<long>("FormDefinitionId")
                        .HasColumnType("bigint");

                    b.Property<long>("FormId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsComplete")
                        .HasColumnType("boolean");

                    b.Property<string>("JsonData")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Metadata")
                        .HasColumnType("text");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("FormDefinitionId");

                    b.HasIndex("FormId");

                    b.ToTable("Submissions", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.Form", b =>
                {
                    b.HasOne("Endatix.Core.Entities.FormDefinition", "ActiveDefinition")
                        .WithOne()
                        .HasForeignKey("Endatix.Core.Entities.Form", "ActiveDefinitionId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("ActiveDefinition");
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormDefinition", b =>
                {
                    b.HasOne("Endatix.Core.Entities.Form", null)
                        .WithMany("FormDefinitions")
                        .HasForeignKey("FormId")
                        .OnDelete(DeleteBehavior.NoAction);
                });

            modelBuilder.Entity("Endatix.Core.Entities.Submission", b =>
                {
                    b.HasOne("Endatix.Core.Entities.FormDefinition", "FormDefinition")
                        .WithMany("Submissions")
                        .HasForeignKey("FormDefinitionId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.OwnsOne("Endatix.Core.Entities.Token", "Token", b1 =>
                        {
                            b1.Property<long>("SubmissionId")
                                .HasColumnType("bigint");

                            b1.Property<DateTime>("ExpiresAt")
                                .HasColumnType("timestamp with time zone");

                            b1.Property<string>("Value")
                                .IsRequired()
                                .HasMaxLength(64)
                                .HasColumnType("character varying(64)");

                            b1.HasKey("SubmissionId");

                            b1.HasIndex("Value");

                            b1.ToTable("Submissions");

                            b1.WithOwner()
                                .HasForeignKey("SubmissionId");
                        });

                    b.Navigation("FormDefinition");

                    b.Navigation("Token");
                });

            modelBuilder.Entity("Endatix.Core.Entities.Form", b =>
                {
                    b.Navigation("FormDefinitions");
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormDefinition", b =>
                {
                    b.Navigation("Submissions");
                });
#pragma warning restore 612, 618
        }
    }
}
