export function Score({ label, score }: { label: string; score: number }) {
  return (
    <div className="score-item">
      <span>{label}</span>
      <strong>{score}<small>%</small></strong>
      <i><b style={{ width: `${score}%` }} /></i>
    </div>
  )
}
