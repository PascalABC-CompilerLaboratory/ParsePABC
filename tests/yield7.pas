var aa: real := 555.0;

type A = class
  testField: real;
  n: real := 83.3;

  function Gen(n: integer): sequence of real;
  var j,k: real;
  begin
    j := 777.0;
    testField := 444.5;
    yield n;
    yield j;
    yield aa;
    yield testField;
    yield self.n;
  end;
end;

begin
  var t := new A();
  foreach var x in t.Gen(5) do
    Print(x);
end.