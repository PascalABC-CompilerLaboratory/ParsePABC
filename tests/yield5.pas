function Gen(n: integer): sequence of real;
var j,k: real;
begin
  j := 777.0;
  yield n;
  yield j;
end;

begin
  foreach var x in Gen(5) do
    Print(x);
end.