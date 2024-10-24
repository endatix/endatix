import React from "react"
import { cn } from "@/lib/utils"

interface DotLoaderProps {
  className?: string
}

const DotLoader =({ className }: DotLoaderProps = {}) => {
  return (
    <div className={cn("flex space-x-1 my-4", className)} aria-label="Loading">
      <div className="w-2 h-2 bg-primary rounded-full animate-bounce" style={{ animationDelay: "0s" }}></div>
      <div className="w-2 h-2 bg-primary rounded-full animate-bounce" style={{ animationDelay: "0.2s" }}></div>
      <div className="w-2 h-2 bg-primary rounded-full animate-bounce" style={{ animationDelay: "0.4s" }}></div>
    </div>
  )
}

export default DotLoader;