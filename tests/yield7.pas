var a: real := 555.0;

type A = class
  testField: real;
  n: real;

  function Gen(n: integer): sequence of real;
  var j,k: real;
  begin
    j := 777.0;
    result := n;
    result := j;
    result := a;
    result := testField;
    result := self.n;
  end;
end;

begin
  var t := new A();
  foreach var x in t.Gen(5) do
    Print(x);
end.